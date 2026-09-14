using DailyRoutines.Common.Module.Abstractions;
using DailyRoutines.Common.Module.Enums;
using DailyRoutines.Common.Module.Models;
using FFXIVAutoBuyer.Universalis;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin.Ipc;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;
using OmenTools.Dalamud.Attributes;
using OmenTools.Interop.Game.Lumina;
using OmenTools.OmenService;

namespace FFXIVAutoBuyer.MarketBoard;

/// <summary>
/// 市场布告板：跨服价格卡片 + 在售物品列表 + 按目标数量购买。
/// 移植自 DailyRoutines 的 BetterMarketBoard 模块（原作者 Fragile）。
/// </summary>
public unsafe partial class MarketBoardModule : ModuleBase
{
    public override ModuleInfo Info { get; } = new()
    {
        Title       = Lang.Get("BetterMarketBoardTitle"),
        Description = Lang.Get("BetterMarketBoardDescription", COMMAND),
        Category    = ModuleCategory.Interface,
        Author      = ["Fragile", "Sparrow-prime"],
        PreviewImageURL =
        [
            "https://gh.atmoomen.top/raw.githubusercontent.com/Dalamud-DailyRoutines/DailyRoutines/main/Resources/Modules/BetterMarketBoard/preview-1.png"
        ]
    };

    public override ModulePermission Permission { get; } = new() { NeedAuth = true, AllDefaultEnabled = true };

    private static InfoProxyItemSearch* InfoProxy => InfoProxyItemSearch.Instance();

    private MarketDataProvider   provider    = null!;
    private Config               config      = null!;
    private LuminaSearcher<Item> searcher    = null!;


    // 大区名 - 数据中心名 - 世界 ID - 世界名
    private Dictionary<string, Dictionary<string, Dictionary<uint, string>>> allWorlds = [];

    private readonly Dictionary<uint, List<Item>> searchCategoryToItems = [];

    private uint lastWorldID;

    /// <summary>上一帧布告板窗口是否打开（用于「刚打开时补一次」）。</summary>
    private bool wasOverlayOpen;

    /// <summary>
    /// 跨服传送通过第三方插件 Lifestream 的 IPC 实现（可选依赖）。
    /// 未安装 Lifestream 时给出提示，不发送聊天指令、也不使用 DR 的 <c>/pdr</c>。
    /// </summary>
    private ICallGateSubscriber<uint, bool>? lifestreamChangeWorldById;

    private ICallGateSubscriber<bool>? lifestreamIsBusy;

    /// <summary>世界切换去抖：连续读到的候选世界与其计数。</summary>
    private uint pendingWorldID;
    private int  pendingWorldTicks;
    private long lastWorldResyncTick;

    /// <summary>确认世界切换所需的连续读取次数（每秒一次）与两次重同步的最小间隔。</summary>
    private const int  WORLD_RESYNC_CONFIRM_TICKS     = 2;
    private const long WORLD_RESYNC_MIN_INTERVAL_MS   = 5_000;

    protected override void Init()
    {
        config = LoadConfig<Config>() ?? new();

        MarketDataProvider.DiagnosticsEnabled = config.EnableDiagnostics;

        uiFontWaitStartTick = Environment.TickCount64;

        if (config.AllWorlds is { Count: > 0 })
            allWorlds = config.AllWorlds;

        Overlay                 ??= new(this);
        Overlay.Flags           =   WINDOW_FLAGS;
        Overlay.WindowName      =   $"{LuminaWrapper.GetPlaceName(1281)}###AutoBuyer-MarketBoard";
        Overlay.SizeConstraints =   new()
        {
            MaximumSize = new(float.MaxValue),
            MinimumSize = ScaledVector2(300f, 200f)
        };

        // 首次打开给一个与原模块相称的默认尺寸（双栏布局），之后沿用玩家自行调整的尺寸
        Overlay.Size          = ScaledVector2(1_000f, 620f);
        Overlay.SizeCondition = ImGuiCond.FirstUseEver;

        var itemsWithCategory =
            LuminaGetter.Get<Item>()
                        .Where(x => !string.IsNullOrEmpty(x.Name.ToString()) && x.ItemSearchCategory.RowId > 0)
                        .GroupBy(x => x.ItemSearchCategory.RowId)
                        .ToDictionary
                        (
                            g => g.Key,
                            g => g.OrderBy(x => x.LevelItem.RowId).ToList()
                        );
        foreach (var (catID, itemList) in itemsWithCategory)
            searchCategoryToItems[catID] = itemList;

        provider = new(this);

        provider.AnchorWorld();
        lastWorldID = GameState.CurrentWorld;

        searcher = new
        (
            [
                .. itemsWithCategory.Values
                                    .SelectMany(x => x)
                                    .GroupBy(x => x.Name.ToString())
                                    .Select(x => x.First())
            ],
            [x => x.Name.ToString(), x => x.RowId.ToString(), x => x.LevelItem.RowId.ToString()]
        );

        TaskHelper = new()
        {
            TaskIntervalMS = 500
        };

        TaskHelper.Enqueue
        (() =>
            {
                _ = RemoteUniversalisCatalog.GetDataCentersOrRequest();
                _ = RemoteUniversalisCatalog.GetWorldsOrRequest();

                if (!RemoteUniversalisCatalog.TryGetDataCenters(out var dataCenters) ||
                    !RemoteUniversalisCatalog.TryGetWorlds(out var worlds))
                    return false;

                var newAllWorlds = BuildAllWorlds(dataCenters, worlds);

                allWorlds        = newAllWorlds;
                config.AllWorlds = newAllWorlds;
                SaveConfig(config);

                provider.AnchorRegion();
                provider.MarkPriceTableDirty();

                return true;
            }
        );

        CommandManager.Instance().AddCommand(COMMAND, new(OnCommand) { HelpMessage = Lang.Get("BetterMarketBoard-CommandHelp") });

        if (IsAbleToSearchMarket()                                              &&
            InfoProxy               != null                                     &&
            InfoProxy->SearchItemId != 0                                        &&
            LuminaGetter.TryGetRow<Item>(InfoProxy->SearchItemId, out var data) &&
            data.ItemSearchCategory.RowId > 0)
        {
            // 只读取游戏当前选中的物品，不改写游戏状态（改写会让游戏自行发起搜索）
            // 只采用游戏当前物品用于界面显示，不主动发起搜索（减少与游戏服务器通信）
            var itemID = InfoProxy->SearchItemId;
            provider.AdoptItemWithoutSearch(itemID, "初始化采用游戏当前物品");
        }

        FrameworkManager.Instance().Reg(OnWorldWatch, 1_000);

        GameState.Instance().MarketListingsStuck += OnMarketListingsStuck;

        TooltipManager.Instance().RegItem(OnItemTooltipUpdate);

        // Lifestream IPC（可选依赖）：仅在玩家右键世界卡片请求传送时使用
        lifestreamChangeWorldById = DService.Instance().PI.GetIpcSubscriber<uint, bool>("Lifestream.ChangeWorldById");
        lifestreamIsBusy           = DService.Instance().PI.GetIpcSubscriber<bool>("Lifestream.IsBusy");
    }

    protected override void Uninit()
    {
        TooltipManager.Instance().Unreg(OnItemTooltipUpdate);
        FrameworkManager.Instance().Unreg(OnWorldWatch);

        GameState.Instance().MarketListingsStuck -= OnMarketListingsStuck;
        CommandManager.Instance().RemoveCommand(COMMAND);

        searchCategoryToItems.Clear();

        provider.ClearAllData();
    }

    #region 事件

    private void OnCommand
    (
        string command,
        string args
    )
    {
        args = args.Trim();

        if (string.IsNullOrEmpty(args))
        {
            ToggleOverlay();
            return;
        }

        if (uint.TryParse(args, out var itemIDInput) && LuminaGetter.TryGetRow<Item>(itemIDInput, out _))
        {
            provider.SelectItem(itemIDInput, reason: "命令-物品ID");
            Overlay.IsOpen = true;
        }
        else
        {
            var firstFound = searcher.Data
                                     .Where(x => x.Name.ToString().Contains(args, StringComparison.OrdinalIgnoreCase))
                                     .OrderBy(x => x.Name.ToString().Length)
                                     .FirstOrDefault();
            if (firstFound.RowId == 0) return;

            provider.SelectItem(firstFound.RowId, reason: "命令-物品名");
            ExecuteOverlaySearch(firstFound.Name.ToString());
            Overlay.IsOpen = true;
        }
    }

    /// <summary>
    /// 请求跨服传送到指定世界（右键世界价格卡片触发）。
    /// 通过 **Lifestream** 的 IPC <c>Lifestream.ChangeWorldById</c> 实现；未安装 / 未启用时提示用户。
    /// </summary>
    public void RequestWorldTravel
    (
        uint   worldID,
        string worldName
    )
    {
        if (worldID == 0 || worldID == GameState.CurrentWorld)
            return;

        var displayName = string.IsNullOrEmpty(worldName) ?
                              LuminaWrapper.GetWorldName(worldID) :
                              worldName;

        try
        {
            if (lifestreamIsBusy?.InvokeFunc() == true)
            {
                NotifyHelper.Instance().NotificationError(Lang.Get("BetterMarketBoard-Travel-Busy"));
                return;
            }

            if (lifestreamChangeWorldById?.InvokeFunc(worldID) == true)
                NotifyHelper.Instance().NotificationSuccess(Lang.Get("BetterMarketBoard-Travel-Requested", displayName));
            else
                NotifyHelper.Instance().NotificationError(Lang.Get("BetterMarketBoard-Travel-Failed", displayName));
        }
        catch (Exception ex)
        {
            // 未安装 / 未启用 Lifestream：IPC 调用会抛异常
            MarketDataProvider.DiagLog($"Lifestream IPC 调用失败：{ex.Message}");

            NotifyHelper.Instance().NotificationError(Lang.Get("BetterMarketBoard-Travel-Failed", displayName));
        }
    }

    /// <summary>插件加载时刻：用于把字体构建门控限制在加载后的最初一段时间内。</summary>
    private static long uiFontWaitStartTick;

    /// <summary>
    /// 取界面字体句柄（分档字号：scale × <c>FontManagerConfig.FontSize</c>，默认 20）。
    /// 界面各处统一走此方法，便于整体调整字号。
    /// </summary>
    public static IFontHandle UIFont
    (
        float scale = 1f
    ) =>
        FontManager.Instance().GetUIFont(scale);

    /// <summary>界面字体的实际像素大小。</summary>
    public static float UIFontSize
    (
        float scale = 1f
    ) =>
        FontManager.Instance().GetActualFontSize(scale);

    /// <summary>把 Universalis 的数据中心 / 世界目录整理为「大区 → 数据中心 → (世界 ID → 世界名)」。</summary>
    private static Dictionary<string, Dictionary<string, Dictionary<uint, string>>> BuildAllWorlds
    (
        List<UniversalisDataCenter> dataCenters,
        List<UniversalisWorld>      worlds
    ) =>
        dataCenters
            .GroupBy(dc => dc.Region)
            .ToDictionary
            (
                region => region.Key,
                region => region
                    .ToDictionary
                    (
                        dc => dc.Name,
                        dc => dc.Worlds.ToDictionary
                        (
                            worldID => worldID,
                            worldID => worlds.FirstOrDefault(w => w.ID == worldID)?.Name ?? string.Empty
                        )
                    )
            );

    /// <summary>
    /// 世界目录自愈：若首次拉取因限流/失败而不完整，配置中的世界列表会长期缺少新世界
    /// （例如国服后开的 沃仙曦染 / 晨曦王座），导致这些世界一直没有价格数据。
    /// 这里在窗口打开时周期性检查：目录一旦就绪且与本地缓存不同，立即刷新并重建价格表。
    /// </summary>
    private void EnsureWorldCatalog()
    {
        if (!RemoteUniversalisCatalog.TryGetDataCenters(out var dataCenters) ||
            !RemoteUniversalisCatalog.TryGetWorlds(out var worlds))
        {
            // 未就绪时尝试拉取（内部有失败冷却，不会形成重试风暴）
            _ = RemoteUniversalisCatalog.GetDataCentersOrRequest();
            _ = RemoteUniversalisCatalog.GetWorldsOrRequest();
            return;
        }

        var newAllWorlds = BuildAllWorlds(dataCenters, worlds);

        if (IsSameWorldSet(allWorlds, newAllWorlds))
            return;

        MarketDataProvider.DiagLog("检测到世界目录变化，已刷新世界列表（新增/变更的世界将纳入价格统计）");

        allWorlds        = newAllWorlds;
        config.AllWorlds = newAllWorlds;
        SaveConfig(config);

        provider.AnchorRegion();
        provider.MarkPriceTableDirty();
    }

    /// <summary>比较两份世界目录的「大区 / 数据中心 / 世界 ID」集合是否一致。</summary>
    private static bool IsSameWorldSet
    (
        Dictionary<string, Dictionary<string, Dictionary<uint, string>>> left,
        Dictionary<string, Dictionary<string, Dictionary<uint, string>>> right
    )
    {
        if (left.Count != right.Count)
            return false;

        foreach (var (regionName, dcs) in right)
        {
            if (!left.TryGetValue(regionName, out var leftDCs) || leftDCs.Count != dcs.Count)
                return false;

            foreach (var (dcName, worldsInDC) in dcs)
            {
                if (!leftDCs.TryGetValue(dcName, out var leftWorlds) || leftWorlds.Count != worldsInDC.Count)
                    return false;

                if (worldsInDC.Keys.Any(worldID => !leftWorlds.ContainsKey(worldID)))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 服务器拒绝了市场数据请求（错误码见日志，游戏提示「请稍后再次确认」）。
    /// 交给数据层进入静默冷却，避免「请求 → 被拒 → 再请求」的刷新循环。
    /// </summary>
    private void OnMarketListingsStuck
    (
        int errorCode
    ) =>
        provider.NotifyMarketRequestRejected();

    /// <summary>
    /// 每秒检查世界是否变化。
    /// 过场/跨服过程中 <see cref="GameState.CurrentWorld"/> 会出现抖动（例如短暂读到 0 或反复跳变），
    /// 若每跳变一次就重同步，会连续触发 <c>ClearListData + 重新搜索</c>，表现为「跨服后持续刷新」。
    /// 因此这里要求：忽略 0；同一个新世界连续 2 次读到才确认切换；且两次重同步之间至少间隔
    /// <see cref="WORLD_RESYNC_MIN_INTERVAL_MS"/>。
    /// </summary>
    private void OnWorldWatch
    (
        IFramework framework
    )
    {
        if (!IsAbleToSearchMarket())
            return;

        var worldID = GameState.CurrentWorld;

        if (worldID == 0)
            return;

        if (lastWorldID == 0)
        {
            // 首次运行：仅记录基线，不做失效处理
            lastWorldID       = worldID;
            pendingWorldID    = 0;
            pendingWorldTicks = 0;
        }
        else if (worldID == lastWorldID)
        {
            pendingWorldID    = 0;
            pendingWorldTicks = 0;
        }
        else
        {
            if (worldID == pendingWorldID)
                pendingWorldTicks++;
            else
            {
                pendingWorldID    = worldID;
                pendingWorldTicks = 1;

                // 首次发现世界变化：立刻作废旧世界数据（不去抖、不发请求），
                // 避免跨服瞬间把上一服务器的挂牌当作本服数据显示
                provider.InvalidateWorldData($"检测到世界变化 {lastWorldID} → {worldID}");
            }

            if (pendingWorldTicks >= WORLD_RESYNC_CONFIRM_TICKS &&
                Environment.TickCount64 - lastWorldResyncTick >= WORLD_RESYNC_MIN_INTERVAL_MS)
            {
                lastWorldID          = worldID;
                lastWorldResyncTick  = Environment.TickCount64;
                pendingWorldID       = 0;
                pendingWorldTicks    = 0;

                provider.ResyncAfterWorldChange();
            }
        }

        // ── 通信最小化：只在布告板窗口正在显示时才做任何自动请求 ──
        // 目的：减少与游戏服务器 / Universalis 的通信，避免被判定为脚本或滥用。
        var isOverlayOpen = Overlay is { IsOpen: true };

        if (isOverlayOpen && !wasOverlayOpen && provider.SelectedItemID != 0)
            provider.RequestRefreshOnce("打开布告板"); // 玩家刚打开窗口：只补一次

        wasOverlayOpen = isOverlayOpen;

        if (!isOverlayOpen)
            return;

        // 世界目录自愈（若首次拉取因限流失败，补齐新增世界）
        EnsureWorldCatalog();

        provider.RetryLocalSearchIfStale();

        // 逐步补齐跨世界价格：每次最多发起少量请求，避免一次性 28 连发（Universalis 429）
        if (provider.SelectedItemID != 0)
            provider.EnsurePriceData(provider.SelectedItemID, provider.HQOnly);
    }

    #endregion

    #region 工具

    private void ExecuteOverlaySearch
    (
        string searchInput
    )
    {
        itemSearchInput = searchInput;
        currentTab      = ItemSelectorTab.Search;
        searcher.Search(searchInput);
    }

    private void ToggleOverlay
    (
        bool? isOpen = null
    )
    {
        provider.EnsureAnchored();

        if (isOpen == null)
            Overlay.IsOpen ^= true;
        else
            Overlay.IsOpen = isOpen.Value;

        Overlay.Collapsed = false;
    }

    #endregion

    #region 预置数据

    private const string COMMAND = "/market";

    private const ImGuiWindowFlags WINDOW_FLAGS = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

    private static readonly List<ItemSearchCategory> ValidCategories =
    [
        .. LuminaGetter.Get<ItemSearchCategory>()
                       .Where(x => !string.IsNullOrEmpty(x.Name.ToString()) && x.Category > 0)
    ];

    #endregion

    #region IPC

    /// <summary>供插件入口（安装器「打开主界面」按钮）与外部 IPC 使用：开关布告板窗口。</summary>
    public bool ToggleOverlayPublic(bool? isOpen = null) =>
        ToggleOverlayIPC(isOpen);

    /// <summary>供插件入口（「设置」按钮）使用：开关模块配置窗口。</summary>
    public bool ToggleConfigPublic(bool? isOpen = null)
    {
        if (!WithConfigUI) return false;

        ToggleOverlayConfig(isOpen);
        return true;
    }

    [IPCProvider("FFXIVAutoBuyer.MarketBoard.SearchItem")]
    private bool SearchItemIPC
    (
        uint itemID
    )
    {
        if (itemID == 0) return false;

        provider.AnchorWorld();
        provider.SelectItem(itemID, reason: "IPC");
        return true;
    }

    [IPCProvider("FFXIVAutoBuyer.MarketBoard.ToggleOverlay")]
    private bool ToggleOverlayIPC(bool? isOpen)
    {
        if (Overlay == null) return false;

        ToggleOverlay(isOpen);
        return true;
    }

    #endregion
}
