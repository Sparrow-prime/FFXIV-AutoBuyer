using FFXIVClientStructs.FFXIV.Client.Game;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;
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

    /// <summary>
    /// 水晶类物品（碎晶 / 水晶 / 晶簇）的绝对持有上限：单格 9999。
    /// </summary>
    private const uint CRYSTAL_STACK_LIMIT = 9999;

    /// <summary>
    /// 是否为水晶类物品：存放于水晶专用背包、可堆叠上限为 9999。
    /// 判据取「堆叠上限 = 9999」（数据驱动，不硬编码分类行号）。
    /// </summary>
    private static bool IsCrystalItem
    (
        uint itemID
    ) =>
        LuminaGetter.TryGetRow<Item>(itemID, out var item) && item.StackSize >= CRYSTAL_STACK_LIMIT;

    /// <summary>
    /// 水晶类的「绝对容量」满包判定：持有量 + 本单数量超过 9999 时必定收不下
    /// （再多也只会溢出），因此直接判失败，不必发出注定被拒的购买请求。
    /// </summary>
    private static bool IsCrystalCapacityExceeded
    (
        uint itemID,
        uint heldCount,
        uint nextQuantity
    ) =>
        nextQuantity != 0 &&
        IsCrystalItem(itemID) &&
        (ulong)heldCount + nextQuantity > CRYSTAL_STACK_LIMIT;

    /// <summary>
    /// 是否**确实**无法收到该物品（用于避免发出注定失败的购买请求）。
    /// 不复用 OmenTools 的 <c>IsFull()</c>：容器未加载或 manager 为空时它会返回 true，
    /// 跨服 / 过场后必然误报「背包已满或无法继续购买」；同时本判定允许与已有同类堆叠。
    /// 判定不出来（容器未加载）时一律视为「可以收到」，宁可让游戏去拒绝。
    /// </summary>
    private static bool IsUnableToReceiveItem(uint itemID)
    {
        var manager = InventoryManager.Instance();
        if (manager == null || itemID == 0) return false;

        var anyContainerLoaded = false;
        var hasSameItemSlot    = false;
        var loadedContainers   = new List<string>();

        foreach (var type in PlayerInventoryTypes)
        {
            var container = manager->GetInventoryContainer(type);
            if (container == null || !container->IsLoaded) continue;

            anyContainerLoaded = true;
            loadedContainers.Add(type.ToString());

            for (var index = 0; index < container->Size; index++)
            {
                var slot = container->GetInventorySlot(index);
                if (slot == null) continue;

                // 有空位 → 一定收得下
                if (slot->ItemId == 0) return false;

                // 身上已有同类物品 → 可能堆叠成功，不阻断
                if (slot->ItemId == itemID) hasSameItemSlot = true;
            }
        }

        // 只有「读到了容器、既无空位也无同类」才判定为收不下
        var isUnable = anyContainerLoaded && !hasSameItemSlot;

        if (isUnable)
            MarketDataProvider.DiagLog($"判定收不下 item={itemID} 已读容器=[{string.Join(",", loadedContainers)}]");

        return isUnable;
    }

    /// <summary>
    /// 需要检查空位的玩家容器。
    /// 注意必须包含 <see cref="InventoryType.Crystals"/>：水晶 / 碎晶 / 晶簇存放在
    /// 水晶专用背包，若只检查 4 个主背包，主背包满 + 主背包内无同类时会误判「收不下」。
    /// </summary>
    private static readonly InventoryType[] PlayerInventoryTypes =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
        InventoryType.Crystals
    ];

    /// <summary>目标数量输入框宽度（按需求：原 90 的一半）。</summary>
    private const float PurchaseTargetInputWidth = 45f;

    /// <summary>
    /// 点击购买后，若游戏侧在售数据尚未就绪（跨服过渡 / 正在重新搜索），
    /// 最多等待这么久；期间会主动请求一次刷新，数据一到立即开买。
    /// </summary>
    private const long PURCHASE_DATA_WAIT_MS = 8_000;

    /// <summary>
    /// 等待单次购买生效的最长时间（毫秒）：超过即判为失败并提示，故取值偏短以便尽快反馈。
    /// 正常购买结果在数百毫秒内返回（游戏侧列表会少一行 / 持有量增加），
    /// 任务轮询间隔为 500ms，2 秒足够覆盖 4 次判定。
    /// </summary>
    private const long PURCHASE_WAIT_MS = 2_000;

    /// <summary>两次购买请求之间的间隔，留给游戏处理与列表刷新，避免连续下单。</summary>
    private const long PURCHASE_COOLDOWN_MS = 350;

    /// <summary>隐式刷新的最小间隔：避免购买过程中反复重新搜索导致列表持续刷新。</summary>
    /// <summary>单个购买任务的最大购买次数，避免异常情况下无限循环。</summary>
    private const int PURCHASE_MAX_ATTEMPTS = 500;

    private bool isPurchasing;
    private uint purchaseItemID;
    private uint purchaseTarget;
    private uint purchaseHeldBefore;
    private int  purchaseAttempts;
    private long purchaseWaitStart;
    private long purchaseDataWaitStart;
    private long purchaseCooldownUntil;
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

            ImGui.SetNextItemWidth(PurchaseTargetInputWidth);

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

            // 购买按钮：底色由「纯亮橙」改为「暗橙」，并显式指定文字颜色，避免刺眼且看不清。
            // 不做「数据未就绪就置灰」的限制：随时可点，数据未到齐时购买流程会等待数据（见 PurchaseStep）
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
        var inputWidth  = PurchaseTargetInputWidth;
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
        provider.AutoSearchSuppressed = true;   // 购买期间禁止一切自动搜索

        purchaseItemID           = itemID;
        purchaseTarget           = targetQuantity;
        purchaseHeldBefore       = LocalPlayerState.GetItemCount(itemID);
        purchaseAttempts         = 0;
        purchaseWaitStart        = 0;
        purchaseDataWaitStart    = 0;
        purchaseCooldownUntil    = 0;
        purchaseWaitingListingID = 0;

        var helper = TaskHelper;

        if (helper == null)
        {
            isPurchasing = false;
            return;
        }

        helper.Enqueue(PurchaseStep, "市场布告板-按目标数量购买", 300_000, weight: 10);
    }

    /// <summary>购买任务主体；返回 false 表示下一帧继续。</summary>
    private bool PurchaseStep()
    {
        if (!isPurchasing)
            return true;

        var info = InfoProxy;

        if (info == null)
        {
            FinishPurchase(Lang.Get("BetterMarketBoard-Purchase-NeedLocalWorld"));
            return true;
        }

        // 0) 游戏侧完全没有这个物品的数据（跨服过渡 / 还没搜索过）：
        //    不直接判失败，而是等待数据 —— 并主动请求一次刷新，数据一到立刻开买。
        //    注意：**不要**把「IsFullyReceived 为假」也算作未就绪 —— 购买成功后游戏侧
        //    会短暂进入该状态（它自己在更新列表），若在此触发刷新，就会表现为
        //    「购买后又刷新了一遍列表」。
        if (info->SearchItemId != purchaseItemID)
        {
            var nowWaiting = Environment.TickCount64;

            if (purchaseDataWaitStart == 0)
            {
                purchaseDataWaitStart = nowWaiting;

                MarketDataProvider.DiagLog($"购买等待数据 item={purchaseItemID}");

                provider.RequestRefreshOnce("购买等待数据");

                NotifyHelper.Instance().NotificationInfo(Lang.Get("BetterMarketBoard-Purchase-WaitWorldData"));
            }

            if (nowWaiting - purchaseDataWaitStart <= PURCHASE_DATA_WAIT_MS)
                return false;

            MarketDataProvider.DiagLog($"购买等待数据超时 item={purchaseItemID}");

            FinishPurchase(Lang.Get("BetterMarketBoard-Purchase-WaitWorldData"));
            return true;
        }

        purchaseDataWaitStart = 0;

        if (!IsPurchaseAvailable(purchaseItemID))
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
                // 该挂单已被买走/买完：立即从显示列表移除（游戏侧数据刷新前也能看到效果）
                var purchasedListingID = purchaseWaitingListingID;

                purchaseWaitStart        = 0;
                purchaseWaitingListingID = 0;

                // 保守策略（用户选择）：购买后**不**自动刷新列表，
                // 仅本地移除已购挂单；数据只在「打开窗口 / 手动刷新」时获取。
                provider.MarkListingPurchased(purchasedListingID);

                // 两次下单之间留出冷却时间，让游戏处理购买并更新列表
                purchaseCooldownUntil = Environment.TickCount64 + PURCHASE_COOLDOWN_MS;
                return false;
            }

            if (Environment.TickCount64 - purchaseWaitStart > PURCHASE_WAIT_MS)
                FinishPurchase
                (
                    IsUnableToReceiveItem(purchaseItemID) ?
                        Lang.Get("BetterMarketBoard-Purchase-Failed-InventoryFull") :
                        Lang.Get("BetterMarketBoard-Purchase-Timeout")
                );

            return !isPurchasing;
        }

        // 冷却中：等游戏把上一次购买与列表更新处理完再继续
        if (Environment.TickCount64 < purchaseCooldownUntil)
            return false;

        // 3) 确实收不下 → 立即停止（不发出版本注定失败的请求，玩家立刻看到提示）
        if (IsUnableToReceiveItem(purchaseItemID))
        {
            FinishPurchase(Lang.Get("BetterMarketBoard-Purchase-Failed-InventoryFull"));
            return true;
        }

        // 4) 取当前服务器在售列表中价格最低的第一条（列表已按单价升序）
        var firstListing = FindCheapestListing(info, purchaseItemID);

        if (firstListing == null)
        {
            FinishPurchase(Lang.Get("BetterMarketBoard-Purchase-NoListing"));
            return true;
        }

        // 4.1) 水晶类专属满包判定：持有量 + 本单数量超过 9999 绝对上限 → 直接失败
        //      （水晶背包单格上限 9999，超出后游戏必定拒绝，发出请求只是白等）
        if (IsCrystalCapacityExceeded(purchaseItemID, heldCount, firstListing.Value.Quantity))
        {
            MarketDataProvider.DiagLog
            (
                $"水晶类超上限：持有 {heldCount} + 本单 {firstListing.Value.Quantity} > {CRYSTAL_STACK_LIMIT}，直接判失败"
            );

            FinishPurchase(Lang.Get("BetterMarketBoard-Purchase-Failed-StackLimit"));
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
        {
            // 游戏侧拒绝下发（多为该挂单已售出/不可购买）：
            // 结束本次购买，但**不**把它计入「已购」——挂单可能仍在售，隐藏会误导
            FinishPurchase(Lang.Get("BetterMarketBoard-Purchase-NoListing"));
        }

        return !isPurchasing;
    }

    /// <summary>结束购买任务并给出结果提示。</summary>
    private void FinishPurchase
    (
        string? failureReason
    )
    {
        var wasPurchasing = isPurchasing;

        isPurchasing                  = false;
        provider.AutoSearchSuppressed = false;   // 恢复自动搜索
        purchaseWaitStart             = 0;
        purchaseWaitingListingID      = 0;

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
        itemID != 0                       &&
        InfoProxy != null                 &&
        IsAbleToSearchLocalMarket()            &&
        provider.SelectedItemID == itemID &&
        provider.SelectedWorldID == CurrentWorldID;

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

            // 已买走的挂单（游戏侧列表可能尚未刷新）：跳过，避免重复下单
            if (MarketDataProvider.IsListingPurchased(listing.ListingId))
                continue;

            if (result == null || listing.UnitPrice < result.Value.UnitPrice)
                result = listing;
        }

        return result;
    }
}
