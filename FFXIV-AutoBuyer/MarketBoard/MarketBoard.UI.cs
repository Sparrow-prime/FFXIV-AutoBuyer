using System.Numerics;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmenTools.OmenService;

namespace FFXIVAutoBuyer.MarketBoard;

public unsafe partial class MarketBoardModule
{
    private const ImGuiWindowFlags OVERLAY_CHILD_FLAGS = ImGuiWindowFlags.NoBackground |
                                                         ImGuiWindowFlags.NoScrollbar  |
                                                         ImGuiWindowFlags.NoScrollWithMouse;

    private static readonly ItemSelectorTab[] ItemSelectorTabs =
    [
        ItemSelectorTab.Search,
        ItemSelectorTab.Favorite
    ];

    private string                    itemSearchInput          = string.Empty;
    private Vector2                   marketDataTableImageSize = new(32);
    private List<MarketFavoriteItem>? favoriteItemsCache;
    private int                       favoriteItemsVersion;
    private int                       favoriteItemsCacheVersion = -1;
    private bool                      isAllWorldsPriceExpanded;

    /// <summary>已跟随过的游戏侧物品（物品 ID → 最近跟随时刻），用于抑制抖动循环。</summary>
    private readonly Dictionary<uint, long> adoptedGameItems = [];

    /// <summary>跟随游戏侧物品的待确认项（用于过滤搜索过渡期的瞬时读数）。</summary>
    private uint pendingGameItemID;
    private long pendingGameItemSince;

    private long lastGameItemAdoptTick;

    /// <summary>
    /// 游戏侧**确实显示过我方当前所选物品**的一次确认：(物品 ID, 物品纪元)。
    /// <para>
    /// 只有它成立过，之后游戏侧换成别的物品才可能是「玩家在游戏里自己改的」；
    /// 否则游戏侧显示的只是**我们更早请求过、而本次请求尚未被游戏接受**的物品 ——
    /// 此时跟随就会表现为「连续切换物品后选中物品回弹」。
    /// </para>
    /// 物品纪元（<see cref="MarketDataProvider.ItemEpoch"/>）保证「A→B→A」这种回到同一物品的情形也算重新确认。
    /// </summary>
    private (uint ItemID, int Epoch) acknowledgedGameItem;

    /// <summary>跟随游戏物品的最小间隔与同一物品的跟随冷却。</summary>
    /// <summary>
    /// 玩家主动选择物品后的保护期：期间一律不跟随游戏侧物品
    /// （我们自己发起的搜索可能被节流跳过，此时游戏侧仍是上一个物品）。
    /// </summary>
    private const long SYNC_ITEM_GUARD_MS = 4_000;

    /// <summary>需要连续观察到同一个「不同物品」这么久，才认为确实切了物品。</summary>
    private const long SYNC_ITEM_CONFIRM_MS = 1_200;

    private const long GAME_ITEM_ADOPT_MIN_INTERVAL_MS = 3_000;
    private const long GAME_ITEM_ADOPT_COOLDOWN_MS     = 30_000;

    /// <summary>
    /// 字体构建门控的时长上限（毫秒）。仅覆盖插件加载后字体图集的首次构建，
    /// 之后（例如玩家在设置里调整字号重建字体）不再隐藏界面。
    /// </summary>
    private const long FONT_BUILD_WAIT_TIMEOUT_MS = 20_000;

    private ItemSelectorTab currentTab = ItemSelectorTab.Search;

    /// <summary>
    /// 界面字体是否仍在异步构建。
    /// OmenTools 的字体图集在插件加载后于后台构建，构建完成前会以游戏回退字体（Axis18）渲染，
    /// 完成后才切换到正式字型/字号 —— 观感上就是「启动几秒后字体变了一次」。
    /// 这里在构建完成前不绘制内容，使界面只以最终字体呈现。
    /// 注：最终字体是 **Dalamud 原生默认字体**（+ 游戏符号 + FontAwesome），并未引入任何外部字体。
    /// </summary>
    private bool IsUIFontBuilding =>
        uiFontWaitStartTick != 0                                        &&
        Environment.TickCount64 - uiFontWaitStartTick < FONT_BUILD_WAIT_TIMEOUT_MS &&
        FontManager.Instance().IsFontBuilding;

    protected override void ConfigUI()
    {
        if (IsUIFontBuilding)
            return;

        ImGui.TextColored(KnownColor.LightSkyBlue.ToVector4(), Lang.Get("Command"));

        using (ImRaii.PushIndent())
            ImGui.TextUnformatted($"{COMMAND} → {Lang.Get("BetterMarketBoard-CommandHelp")}");

        ImGui.NewLine();

        if (ImGui.Checkbox(Lang.Get("BetterMarketBoard-Config-EnableDiagnostics"), ref config.EnableDiagnostics))
        {
            MarketDataProvider.DiagnosticsEnabled = config.EnableDiagnostics;
            SaveConfig(config);
        }

        ImGuiOm.HelpMarker(Lang.Get("BetterMarketBoard-Config-EnableDiagnostics-Help"));

        ImGui.NewLine();

        if (ImGui.Checkbox(Lang.Get("BetterMarketBoard-Config-NotifyInventoryFull"), ref config.NotifyInventoryFull))
            SaveConfig(config);

        ImGuiOm.HelpMarker(Lang.Get("BetterMarketBoard-Config-NotifyInventoryFull-Help"));

        ImGui.NewLine();

        ImGui.TextColored(KnownColor.LightSkyBlue.ToVector4(), Lang.Get("BetterMarketBoard-Config-FontSize"));

        using (ImRaii.PushIndent())
        {
            var fontSize = FontManager.Instance().Config.FontSize;

            ImGui.SetNextItemWidth(160f * GlobalUIScale);
            ImGui.DragFloat("###AutoBuyer-FontSize", ref fontSize, 0.1f, 16f, 28f, "%.1f");

            // 仅在拖动结束后应用并重建字体，避免拖动过程中反复重建
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                FontManager.Instance().Config.FontSize = fontSize;
                FontManager.Instance().Config.Save();

                _ = FontManager.Instance().RebuildUIFontsAsync();
            }

            ImGuiOm.HelpMarker(Lang.Get("BetterMarketBoard-Config-FontSize-Help"));
        }
    }

    protected override void OverlayUI()
    {
        // 字体尚未构建完成时先不绘制，避免「回退字体 → 正式字体」的突变（详见 IsUIFontBuilding）
        if (IsUIFontBuilding)
            return;

        Overlay.CollapsedCondition = ImGuiCond.None;
        Overlay.Collapsed          = null;

        SyncItemWithGame();

        var frame = CreateFrameContext();

        using var table = ImRaii.Table("Table", 2, ImGuiTableFlags.Resizable);
        if (!table) return;

        ImGui.TableSetupColumn("Left",  ImGuiTableColumnFlags.WidthFixed, 220f * GlobalUIScale);
        ImGui.TableSetupColumn("Right", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextRow();

        ImGui.TableNextColumn();

        using (var child = ImRaii.Child("###Left", new(-1f), true, OVERLAY_CHILD_FLAGS))
        {
            if (child)
                DrawLeftPanel(frame);
        }

        ImGui.TableNextColumn();

        using (var child = ImRaii.Child("###Right", new(-1f), true, OVERLAY_CHILD_FLAGS))
        {
            if (child)
                DrawRightContent(frame);
        }
    }

    private MarketBoardUIContext CreateFrameContext()
    {
        var itemID = provider.SelectedItemID;

        var   hasItem     = false;
        var   itemData    = default(Item);
        uint? npcGilPrice = null;

        if (itemID != 0 && LuminaGetter.TryGetRow<Item>(itemID, out var row))
        {
            hasItem     = true;
            itemData    = row;
            npcGilPrice = provider.GetNPCGilPrice(itemID);
        }

        return new
        (
            provider,
            this,
            itemID,
            hasItem,
            itemData,
            npcGilPrice,
            CurrentWorldID,
            provider.SelectedWorldID,
            provider.HQOnly,
            IsAbleToSearchLocalMarket()
        );
    }

    /// <summary>
    /// 跟随游戏侧市场列表当前选中的物品。
    /// 游戏侧 <c>SearchItemId</c> 会因自身刷新/被服务器拒绝而抖动，
    /// 若每次都跟随会形成「我们重选 → 游戏回写 → 我们再重选」的循环（表现为列表高速刷新、
    /// 不断弹出「请稍后再次确认」），因此这里加了：繁忙不跟随、最小间隔、同一物品冷却期内只跟随一次。
    /// </summary>
    private void SyncItemWithGame()
    {
        // 服务器刚拒绝过市场数据请求时不跟随
        if (GameState.Instance().IsMarketListingsStuck) return;

        var infoProxy = InfoProxy;

        var proxyItemID = infoProxy == null ?
                              0u :
                              infoProxy->SearchItemId;

        if (proxyItemID == 0 || proxyItemID == provider.SelectedItemID)
        {
            // 游戏侧确实显示着我方当前所选物品 → 记为「本次选择已被游戏确认」
            if (proxyItemID != 0)
                acknowledgedGameItem = (proxyItemID, provider.ItemEpoch);

            pendingGameItemID = 0;
            return;
        }

        var now = Environment.TickCount64;

        if (now - provider.LastSelectTime < SYNC_ITEM_GUARD_MS)
        {
            MarketDataProvider.DiagLog($"跟随游戏物品被跳过（选择保护期内）game={proxyItemID}");
            return;
        }

        // 我方所选物品的搜索还没完成（请求被节流/在途等待跳过）：
        // 此时游戏侧仍是上一个物品，跟随会导致「跳回原来的物品」
        if (provider.IsLocalListingsStale)
        {
            MarketDataProvider.DiagLog($"跟随游戏物品被跳过（我方搜索未完成）game={proxyItemID}");
            return;
        }

        // 我方这次的所选物品**从未被游戏侧接受过**（游戏侧显示的是更早请求过的物品）→ 不跟随。
        // 这是「连续快速切换物品后，选中物品回弹到更早那个物品」的根因：
        // 快速切换时中间若干次搜索请求会被节流/在途判定跳过，游戏侧一直停在较早的物品上，
        // 若此时跟随，就会把玩家的最新选择改回旧物品。
        if (acknowledgedGameItem != (provider.SelectedItemID, provider.ItemEpoch))
        {
            MarketDataProvider.DiagLog
            (
                $"跟随游戏物品被跳过（我方所选物品尚未被游戏确认）game={proxyItemID} ours={provider.SelectedItemID} 上次确认={acknowledgedGameItem}"
            );

            return;
        }

        // 需要连续观察到同一个「不同物品」才采用，避免搜索过渡期的瞬时读数
        if (proxyItemID != pendingGameItemID)
        {
            pendingGameItemID    = proxyItemID;
            pendingGameItemSince = now;
            return;
        }

        if (now - pendingGameItemSince < SYNC_ITEM_CONFIRM_MS) return;

        if (now - lastGameItemAdoptTick < GAME_ITEM_ADOPT_MIN_INTERVAL_MS) return;

        // 同一物品在冷却期内只跟随一次
        if (adoptedGameItems.TryGetValue(proxyItemID, out var lastAdoptedTick) &&
            now - lastAdoptedTick < GAME_ITEM_ADOPT_COOLDOWN_MS)
            return;

        if (!LuminaGetter.TryGetRow<Item>(proxyItemID, out var item) || item.ItemSearchCategory.RowId == 0)
            return;

        adoptedGameItems[proxyItemID] = now;
        lastGameItemAdoptTick         = now;
        pendingGameItemID             = 0;

        MarketDataProvider.DiagLog($"跟随游戏侧物品 game={proxyItemID}（我方={provider.SelectedItemID}）");

        provider.SelectItem(proxyItemID, reason: "同步游戏侧物品");
    }

    private readonly struct MarketBoardUIContext
    (
        MarketDataProvider provider,
        MarketBoardModule  owner,
        uint               itemID,
        bool               hasItem,
        Item               itemData,
        uint?              npcGilPrice,
        uint               currentWorldID,
        uint               selectedWorldID,
        bool               hqOnly,
        bool               isLocalMarketSearchable
    )
    {
        public MarketDataProvider Provider { get; } = provider;
        public MarketBoardModule  Owner    { get; } = owner;

        public uint  ItemID                  { get; } = itemID;
        public bool  HasItem                 { get; } = hasItem;
        public Item  ItemData                { get; } = itemData;
        public uint? NPCGilPrice             { get; } = npcGilPrice;
        public uint  CurrentWorldID          { get; } = currentWorldID;
        public uint  SelectedWorldID         { get; } = selectedWorldID;
        public bool  HQOnly                  { get; } = hqOnly;
        public bool  IsLocalMarketSearchable { get; } = isLocalMarketSearchable;

        public bool IsViewingCurrentWorld =>
            SelectedWorldID == CurrentWorldID;
    }
}
