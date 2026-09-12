using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using OmenTools.Interop.Game.Lumina;
using OmenTools.OmenService;

namespace FFXIVAutoBuyer.MarketBoard;

/// <summary>
/// 顶部购买区：按目标持有数量，从当前服务器在售列表中反复购买最低价条目。
/// </summary>
public unsafe partial class MarketBoardModule
{
    /// <summary>单次购买后等待列表/持有量变化的超时时间。</summary>
    /// <summary>购买按钮配色（暗橙底 + 浅暖色文字：亮度更低、对比更高）。</summary>
    private static readonly Vector4 PurchaseButtonColor        = new(0.45f, 0.24f, 0.06f, 1f);

    private static readonly Vector4 PurchaseButtonHoveredColor = new(0.58f, 0.31f, 0.08f, 1f);
    private static readonly Vector4 PurchaseButtonActiveColor  = new(0.36f, 0.19f, 0.05f, 1f);
    private static readonly Vector4 PurchaseButtonTextColor    = new(1.00f, 0.86f, 0.62f, 1f);

    private const long PURCHASE_WAIT_MS = 5_000;

    /// <summary>两次购买请求之间的间隔，留给游戏处理与列表刷新，避免连续下单。</summary>
    private const long PURCHASE_COOLDOWN_MS = 350;

    /// <summary>隐式刷新的最小间隔：避免购买过程中反复重新搜索导致列表持续刷新。</summary>
    private const long IMPLICIT_REFRESH_MIN_INTERVAL_MS = 2_000;

    /// <summary>单个购买任务的最大购买次数，避免异常情况下无限循环。</summary>
    private const int PURCHASE_MAX_ATTEMPTS = 500;

    private bool isPurchasing;
    private uint purchaseItemID;
    private uint purchaseTarget;
    private uint purchaseHeldBefore;
    private int  purchaseAttempts;
    private long purchaseWaitStart;
    private long purchaseCooldownUntil;
    private long lastImplicitRefreshTick;
    private ulong purchaseWaitingListingID;

    /// <summary>
    /// 绘制「持有数量 / 目标数量 / 购买」控件组。
    /// 与物品名同行并贴右边缘右对齐（调用前需先执行 <c>ImGui.SameLine()</c>）。
    /// </summary>
    private void DrawPurchaseControls
    (
        MarketBoardUIContext frame
    )
    {
        var heldCount = LocalPlayerState.GetItemCount(frame.ItemID);

        using (UIFont(0.8f).Push())
        {
            // 右对齐：按控件组实测宽度把游标推到右边缘
            var groupWidth = MeasurePurchaseControlsWidth(heldCount);
            var targetX    = ImGui.GetWindowContentRegionMax().X - groupWidth;

            if (ImGui.GetCursorPosX() < targetX)
                ImGui.SetCursorPosX(targetX);

            // 持有数量（实时数字）
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored
            (
                heldCount > 0 ?
                    KnownColor.LightSkyBlue.ToVector4() :
                    KnownColor.White.ToVector4(),
                $"{Lang.Get("BetterMarketBoard-Purchase-Held")} {heldCount}"
            );
            ImGuiOm.TooltipHover($"{LuminaWrapper.GetAddonText(358)}");

            ImGui.SameLine();

            // 目标数量输入框
            var target = (int)Math.Min(config.PurchaseQuantity, int.MaxValue);

            ImGui.SetNextItemWidth(90f * GlobalUIScale);

            if (ImGui.InputInt("###PurchaseTarget", ref target, 0, 0))
            {
                var clamped   = Math.Clamp(target, 0, 9999);
                var newTarget = (uint)clamped;

                if (newTarget != config.PurchaseQuantity)
                {
                    config.PurchaseQuantity = newTarget;
                    SaveConfig(config);
                }
            }

            ImGuiOm.TooltipHover(Lang.Get("BetterMarketBoard-Purchase-Target"));

            ImGui.SameLine();

            // 购买按钮：底色由「纯亮橙」改为「暗橙」，并显式指定文字颜色，避免刺眼且看不清
            var canPurchase = !isPurchasing;

            using (ImRaii.Disabled(!canPurchase))
            using (ImRaii.PushColor(ImGuiCol.Button,        PurchaseButtonColor,        canPurchase))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, PurchaseButtonHoveredColor, canPurchase))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive,  PurchaseButtonActiveColor,  canPurchase))
            using (ImRaii.PushColor(ImGuiCol.Text,          PurchaseButtonTextColor,    canPurchase))
            {
                var buttonLabel = GetPurchaseButtonLabel();
                var buttonSize  = new Vector2
                (
                    ImGui.CalcTextSize(buttonLabel).X + (ImGui.GetStyle().FramePadding.X * 2f),
                    ImGui.GetTextLineHeight() + (ImGui.GetStyle().FramePadding.Y * 2f)
                );

                if (ImGui.Button(buttonLabel, buttonSize))
                {
                    if (isPurchasing)
                        FinishPurchase(Lang.Get("BetterMarketBoard-Purchase-Stopped-Manual"));
                    else
                        StartPurchase(frame.ItemID, config.PurchaseQuantity);
                }
            }

            // 购买按钮的说明：与列表行的右键快捷购买提示区分开（后者挂在列表行上）
            ImGuiOm.TooltipHover
            (
                isPurchasing ?
                    Lang.Get("BetterMarketBoard-Purchase-InProgress-Help") :
                    Lang.Get("BetterMarketBoard-Purchase-Button-Help", config.PurchaseQuantity)
            );
        }
    }

    /// <summary>购买按钮当前文字（用于绘制与宽度测算保持一致）。</summary>
    private string GetPurchaseButtonLabel() =>
        isPurchasing ?
            $"{FontAwesomeIcon.Stopwatch.ToIconString()} {Lang.Get("BetterMarketBoard-Purchase-InProgress")}" :
            $"{FontAwesomeIcon.ShoppingCart.ToIconString()} {Lang.Get("BetterMarketBoard-Purchase-Button")}";

    /// <summary>当前字号下购买控件组的总宽度（持有数量 + 输入框 + 按钮 + 间距），用于右对齐。</summary>
    private float MeasurePurchaseControlsWidth
    (
        uint heldCount
    )
    {
        var style = ImGui.GetStyle();

        var heldWidth   = ImGui.CalcTextSize($"{Lang.Get("BetterMarketBoard-Purchase-Held")} {heldCount}").X;
        var inputWidth  = 90f * GlobalUIScale;
        var buttonWidth = ImGui.CalcTextSize(GetPurchaseButtonLabel()).X + (style.FramePadding.X * 2);

        return heldWidth + inputWidth + buttonWidth + (style.ItemSpacing.X * 2);
    }

    /// <summary>开始按目标数量购买。</summary>
    private void StartPurchase
    (
        uint itemID,
        uint targetQuantity
    )
    {
        if (isPurchasing || itemID == 0) return;

        if (targetQuantity == 0)
        {
            NotifyHelper.Instance().NotificationError(Lang.Get("BetterMarketBoard-Purchase-InvalidTarget"));
            return;
        }

        if (!IsPurchaseAvailable(itemID))
        {
            NotifyHelper.Instance().NotificationError(Lang.Get("BetterMarketBoard-Purchase-NeedLocalWorld"));
            return;
        }

        isPurchasing             = true;
        purchaseItemID           = itemID;
        purchaseTarget           = targetQuantity;
        purchaseHeldBefore       = LocalPlayerState.GetItemCount(itemID);
        purchaseAttempts         = 0;
        purchaseWaitStart        = 0;
        purchaseCooldownUntil    = 0;
        lastImplicitRefreshTick  = 0;
        purchaseWaitingListingID = 0;

        var helper = TaskHelper;

        if (helper == null)
        {
            isPurchasing = false;
            return;
        }

        helper.Enqueue(PurchaseStep, "市场布告板-按目标数量购买", 300_000, weight: 10);
    }

    /// <summary>
    /// 隐式刷新（限流）：两次调用间隔小于 <see cref="IMPLICIT_REFRESH_MIN_INTERVAL_MS"/> 时跳过，
    /// 避免购买流程高频触发市场搜索请求，导致游戏列表持续刷新。
    /// </summary>
    private void TryImplicitRefresh()
    {
        var now = Environment.TickCount64;

        if (now - lastImplicitRefreshTick < IMPLICIT_REFRESH_MIN_INTERVAL_MS)
            return;

        lastImplicitRefreshTick = now;

        provider.BeginImplicitRefresh(purchaseItemID);
    }

    /// <summary>购买任务主体；返回 false 表示下一帧继续。</summary>
    private bool PurchaseStep()
    {
        if (!isPurchasing)
            return true;

        var info = InfoProxy;

        if (info == null || !IsPurchaseAvailable(purchaseItemID))
        {
            FinishPurchase(Lang.Get("BetterMarketBoard-Purchase-NeedLocalWorld"));
            return true;
        }

        // 1) 达到目标数量 → 完成
        var heldCount = LocalPlayerState.GetItemCount(purchaseItemID);

        if (heldCount >= purchaseTarget)
        {
            FinishPurchase(null);
            return true;
        }

        // 2) 正在等待上一次购买生效
        if (purchaseWaitStart != 0)
        {
            var listingGone = !IsListingStillThere(info, purchaseWaitingListingID);

            if (heldCount > purchaseHeldBefore || listingGone)
            {
                purchaseWaitStart        = 0;
                purchaseWaitingListingID = 0;

                // 隐式刷新：不隐藏当前列表；限流调用，避免连续重新搜索导致列表持续刷新
                TryImplicitRefresh();

                // 两次下单之间留出冷却时间，让游戏处理购买并更新列表
                purchaseCooldownUntil = Environment.TickCount64 + PURCHASE_COOLDOWN_MS;
                return false;
            }

            if (Environment.TickCount64 - purchaseWaitStart > PURCHASE_WAIT_MS)
                FinishPurchase(Lang.Get("BetterMarketBoard-Purchase-Failed-InventoryFull"));

            return !isPurchasing;
        }

        // 冷却中：等游戏把上一次购买与列表更新处理完再继续
        if (Environment.TickCount64 < purchaseCooldownUntil)
            return false;

        // 3) 取当前服务器在售列表中价格最低的第一条（列表已按单价升序）
        var firstListing = FindCheapestListing(info, purchaseItemID);

        if (firstListing == null)
        {
            FinishPurchase(Lang.Get("BetterMarketBoard-Purchase-NoListing"));
            return true;
        }

        if (++purchaseAttempts > PURCHASE_MAX_ATTEMPTS)
        {
            FinishPurchase(Lang.Get("BetterMarketBoard-Purchase-Timeout"));
            return true;
        }

        purchaseHeldBefore       = heldCount;
        purchaseWaitingListingID = firstListing.Value.ListingId;
        purchaseWaitStart        = Environment.TickCount64;

        if (!MarketDataProvider.SendBuyRequest(firstListing.Value))
            FinishPurchase(Lang.Get("BetterMarketBoard-Purchase-Failed-InventoryFull"));

        return !isPurchasing;
    }

    /// <summary>结束购买任务并给出结果提示。</summary>
    private void FinishPurchase
    (
        string? failureReason
    )
    {
        var wasPurchasing = isPurchasing;

        isPurchasing             = false;
        purchaseWaitStart        = 0;
        purchaseWaitingListingID = 0;

        if (!wasPurchasing || purchaseItemID == 0)
            return;

        var heldCount = LocalPlayerState.GetItemCount(purchaseItemID);
        var boughtCount = heldCount >= purchaseHeldBefore ?
                              heldCount - purchaseHeldBefore :
                              0;

        if (failureReason == null)
        {
            NotifyHelper.Instance().NotificationSuccess
            (
                Lang.Get("BetterMarketBoard-Purchase-Success", boughtCount, heldCount)
            );
        }
        else
        {
            NotifyHelper.Instance().NotificationError
            (
                Lang.Get("BetterMarketBoard-Purchase-Stopped", failureReason)
            );
        }

        purchaseItemID = 0;
    }

    /// <summary>购买仅对本服在售列表有效。</summary>
    private bool IsPurchaseAvailable
    (
        uint itemID
    ) =>
        itemID != 0                     &&
        InfoProxy != null               &&
        IsAbleToSearchMarket()          &&
        provider.SelectedItemID == itemID &&
        provider.SelectedWorldID == GameState.CurrentWorld;

    private static bool IsListingStillThere
    (
        InfoProxyItemSearch* info,
        ulong                listingID
    )
    {
        foreach (var listing in info->Listings)
        {
            if (listing.ListingId == listingID)
                return true;
        }

        return false;
    }

    private static MarketBoardListing? FindCheapestListing
    (
        InfoProxyItemSearch* info,
        uint                 itemID
    )
    {
        MarketBoardListing? result = null;

        foreach (var listing in info->Listings)
        {
            if (listing.ItemId != itemID || listing.UnitPrice == 0)
                continue;

            if (IsOwnRetainer(listing.RetainerId))
                continue;

            if (result == null || listing.UnitPrice < result.Value.UnitPrice)
                result = listing;
        }

        return result;
    }
}
