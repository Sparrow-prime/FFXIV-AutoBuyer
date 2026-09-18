using DailyRoutines.Common.Module.Abstractions;
using DailyRoutines.Common.Module.Enums;
using DailyRoutines.Common.Module.Models;
using FFXIVAutoBuyer.Universalis;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin.Ipc;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
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

    /// <summary>
    /// 本插件认定的「玩家当前所在世界」。
    /// 加载时只从大厅数据取一次；跨界传送后由游戏日志
    /// 「使用跨界传送移动到了xxx。」权威更新（该行只在**落地后**出现）。
    /// <para>
    /// 为什么不直接用 <c>GameState.CurrentWorld</c>：它取自大厅数据（LobbyData），
    /// 会在过场开始时就变化、也可能根本不随跨界传送变化 —— 一旦与真实世界不一致，
    /// 检测不会触发、世界锚点也会写错，表现为「一直显示上一服务器的数据」。
    /// 因此全插件（数据层 / 购买 / 卡片高亮 / Tooltip / UI 帧）的「当前世界」判定统一以此为准。
    /// </para>
    /// </summary>
    private static uint CurrentWorldID { get; set; }

    /// <summary>上一帧布告板窗口是否打开（用于「刚打开时补一次」）。</summary>
    private bool wasOverlayOpen;

    /// <summary>
    /// 跨服传送通过第三方插件 Lifestream 的 IPC 实现（可选依赖）。
    /// 未安装 Lifestream 时给出提示，不发送聊天指令、也不使用 DR 的 <c>/pdr</c>。
    /// </summary>
    private ICallGateSubscriber<uint, bool>? lifestreamChangeWorldById;

    private ICallGateSubscriber<bool>? lifestreamIsBusy;

    /// <summary>跨界传送日志行的固定文案前缀（除服务器名外无任何变体）。</summary>
    private const string WORLD_VISIT_LOG_PREFIX = "使用跨界传送移动到了";

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

        // 世界锚点：加载时只从大厅数据取一次，之后由跨界传送日志权威更新
        CurrentWorldID = GameState.CurrentWorld;
        provider.AnchorWorld();

        BuildWorldVisitLogMessageIDs();

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

        if (IsAbleToSearchLocalMarket()                                              &&
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

        // 跨界传送判定：游戏日志「使用跨界传送移动到了xxx。」（仅在插件窗口打开时监测）
        LogMessageManager.Instance().RegPost(OnWorldVisitLogMessage);

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
        LogMessageManager.Instance().Unreg(OnWorldVisitLogMessage);

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
        if (worldID == 0 || worldID == CurrentWorldID)
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
    /// 每秒一次的窗口/数据节奏维护。
    /// <para>
    /// 世界变化的判定**不在**这里：改为监听游戏日志「使用跨界传送移动到了xxx。」
    /// （见 <see cref="OnWorldVisitLogMessage"/>），该行只在落地后出现，
    /// 因此命中即可当帧处理，无需任何去抖/延迟等待。
    /// </para>
    /// </summary>
    private void OnWorldWatch
    (
        IFramework framework
    )
    {
        if (!IsAbleToSearchLocalMarket())
            return;

        // ── 通信最小化：只在布告板窗口正在显示时才做任何自动请求 ──
        // 目的：减少与游戏服务器 / Universalis 的通信，避免被判定为脚本或滥用。
        var isOverlayOpen = Overlay is { IsOpen: true };

        if (isOverlayOpen && !wasOverlayOpen)
        {
            // 窗口关闭期间不监测日志 → 开窗时用角色当前世界校正一次（防止跨服后仍显示旧服数据）
            SyncWorldOnWindowOpen();

            // 玩家刚打开窗口：只补一次
            if (provider.SelectedItemID != 0)
                provider.RequestRefreshOnce("打开布告板");
        }

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

    /// <summary>
    /// 跨界传送判定（**仅在插件窗口打开时监测**）。
    /// <para>
    /// 游戏日志出现「使用跨界传送移动到了xxx。」表示玩家**已经落地新世界**，
    /// 因此当帧立刻作废旧世界数据并重同步，不做任何去抖/延迟等待。
    /// 除服务器名外该文案没有其它变体，故只按固定前缀匹配。
    /// </para>
    /// </summary>
    private void OnWorldVisitLogMessage
    (
        uint                logMessageID,
        LogMessageQueueItem item
    )
    {
        if (!IsWorldVisitMonitoringActive()) return;

        // 快路径：先按 LogMessage 行号过滤（模板含固定文案的行），未命中则不看文本
        if (worldVisitLogMessageIDs.Count > 0 && !worldVisitLogMessageIDs.Contains(logMessageID)) return;

        if (!TryParseWorldVisitWorldName(item.ToReadOnlySeString().ToString(), out var worldName))
            return;

        var worldID = ResolveWorldIDByName(worldName);

        if (worldID == 0)
        {
            // 世界名未识别：退到「玩家实际所在世界」（角色结构体 → 大厅数据）
            worldID = ResolvePlayerWorldID();

            MarketDataProvider.DiagLog($"跨界传送日志命中但世界名未识别：\"{worldName}\"，改用玩家当前世界 {worldID}");
        }

        if (worldID == 0) return;

        HandleWorldChange(worldID, worldName, $"跨界传送日志（{logMessageID}）");
    }

    /// <summary>
    /// 窗口打开时校正世界：窗口关闭期间玩家可能已经跨服（此时不监测日志）。
    /// 以角色结构体的当前世界为准（落地后即更新），发现不同即按跨服处理。
    /// </summary>
    private void SyncWorldOnWindowOpen()
    {
        var worldID = ResolvePlayerWorldID();

        if (worldID == 0 || worldID == CurrentWorldID) return;

        HandleWorldChange(worldID, LuminaWrapper.GetWorldName(worldID), "开窗校正");
    }

    /// <summary>
    /// 跨服 / 世界切换的统一处理：更新当前世界 → 作废旧世界数据 → 重同步。
    /// 全流程为本地操作（不发请求），搜索仍由「布告板可用即补」的既有机制驱动。
    /// </summary>
    private void HandleWorldChange
    (
        uint   worldID,
        string worldName,
        string reason
    )
    {
        var previous = CurrentWorldID;
        CurrentWorldID = worldID;

        MarketDataProvider.DiagLog($"世界切换：{previous} → {worldID}（{worldName}）reason={reason}");

        provider.InvalidateWorldData(reason);
        provider.ResyncAfterWorldChange();
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
