using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Hooking;
using Dalamud.IoC;
using OmenTools.Dalamud;
using OmenTools.Dalamud.Services.Game.UI;
using OmenTools.Dalamud.Services.Game.Object;
using OmenTools.Interop.Game;
using OmenTools.OmenService;
using OmenTools.OmenService.Abstractions;
using OmenTools.Threading.TaskHelper;

namespace OmenTools;

public sealed class DService
{
    #region 公开接口

    public static void Init(IDalamudPluginInterface pluginInterface, Func<DServiceInitOptions>? optionsFunc = null)
    {
        if (IsInitialized || IsDisposed) return;

        var instance = Instance();
        pluginInterface.Inject(instance);

        instance.ResetServiceState();

        instance.InitOptions = optionsFunc != null ? optionsFunc() : new();

        instance.PI            = pluginInterface;
        instance.UIBuilder     = pluginInterface.UiBuilder;
        instance.ObjectTable   = new ObjectTable();
        instance.AetheryteList = new AetheryteList();
        
        instance.OmenDalamudServices[typeof(IObjectTable)]   = instance.ObjectTable;
        instance.OmenDalamudServices[typeof(IAetheryteList)] = instance.AetheryteList;

        try
        {
            var serviceTypes = instance.DiscoverEnabledServiceTypes();
            instance.InstantiateServices(serviceTypes);

            foreach (var serviceType in serviceTypes)
            {
                var service = instance.OmenServices[serviceType];
                service.PublicInit();
                instance.initializedServiceOrder.Add(serviceType);
            }

            IsInitialized = true;
        }
        catch (Exception ex)
        {
            DLog.Error("[OmenTools] 初始化各 OmenService 时发生错误", ex);
            Uninit();

            throw;
        }

        var alc   = AssemblyLoadContext.GetLoadContext(typeof(DService).Assembly);
        var owner = pluginInterface.GetPlugin(alc);
        DLog.Debug($"[OmenTools] 初始化完成\tALC: {alc}; 持有方: {owner?.InternalName ?? "<shared>"}");
    }

    public static void Uninit()
    {
        if (IsDisposed)
            return;

        if (InternalInstance == null)
            return;

        try
        {
            InternalInstance.UninitOmenServices();

            InternalInstance.DisposeTrackedTaskHelpers();
            InternalInstance.DisposeTrackedMemoryPatches();
            InternalInstance.DisposeTrackedHooks();

            InternalInstance.ResetServiceState();

            InternalInstance.ObjectTable   = null;
            InternalInstance.AetheryteList = null;
            InternalInstance.OmenDalamudServices.Clear();
        }
        catch (Exception ex)
        {
            DLog.Error("[OmenTools] 卸载各 OmenService 时发生错误", ex);
            throw;
        }
        finally
        {
            IsDisposed = true;
        }

        var alc   = AssemblyLoadContext.GetLoadContext(typeof(DService).Assembly);
        var owner = IDalamudPluginInterface.Instance().GetPlugin(alc);
        DLog.Debug($"[OmenTools] 卸载完成\tALC: {alc}; 持有方: {owner?.InternalName ?? "<shared>"}");
    }

    public static DService Instance() =>
        InternalInstance ??= new();

    public T? GetOmenService<T>() where T : OmenServiceBase =>
        (T?)OmenServices.GetValueOrDefault(typeof(T));
    
    public T? GetOmenDalamudService<T>() where T : IOmenDalamudService =>
        (T?)OmenDalamudServices.GetValueOrDefault(typeof(T));

    #endregion

    #region 生命周期

    public static bool IsDisposed { get; private set; }

    public static bool IsInitialized { get; private set; }

    private static DService? InternalInstance { get; set; }

    #endregion

    #region 私有

    private DServiceInitOptions InitOptions { get; set; } = new();

    private Dictionary<Type, OmenServiceBase>        OmenServices        { get; set; } = [];
    private Dictionary<Type, IOmenDalamudService> OmenDalamudServices { get; set; } = [];

    private ConcurrentDictionary<TaskHelper, byte>   TaskHelpers   { get; set; } = [];
    private ConcurrentDictionary<MemoryPatch, byte>  MemoryPatches { get; set; } = [];
    private ConcurrentDictionary<IDalamudHook, byte> Hooks         { get; set; } = [];

    private List<Type> initializedServiceOrder = [];

    /// <summary>
    /// 【AutoBuyer 本地改动·服务白名单】只有列在这里的服务类型会被实例化并 <c>Init</c>。
    /// <para>
    /// 为什么需要：<c>DService</c> 原本会自动实例化**所有** <c>OmenServiceBase</c> 子类，
    /// 而每个服务的 <c>Init</c> 都可能挂游戏函数 Hook、起后台 tick、订阅事件或发起网络请求。
    /// 本插件内嵌的 OmenTools 是**冻结副本**，但一旦有人（或某次上游同步）往库里放进新的服务文件，
    /// 它就会在插件加载时自动生效 —— 这条「悄悄多干活」的路径必须从根上掐掉。
    /// </para>
    /// <para>
    /// 维护约定：**确有用到某个服务时才加进这张表**，并在 CHANGELOG 里写清「为什么需要它、它 Init 会做什么」。
    /// 不在表内的服务一律不实例化，并会在加载日志里逐个列名（便于发现意外新增）。
    /// </para>
    /// </summary>
    private static readonly Type[] ENABLED_SERVICE_TYPES =
    [
        typeof(FrameworkManager),    // 每帧回调分发（1 秒世界巡检 / 补拉节奏）
        typeof(FontManager),         // 界面字体图集
        typeof(WindowManager),       // Overlay 窗口注册与绘制
        typeof(LocalizationManager), // 词条（zh-CN.json）
        typeof(CommandManager),      // /market 命令
        typeof(LogMessageManager),   // 跨界传送日志判定
        typeof(ItemSourceManager),   // NPC 收购价（ItemSourceInfo.Query）
        typeof(GameState),           // 登录 / 副本 / 市场繁忙状态
        typeof(LocalPlayerState),    // 持有数量等玩家状态
        typeof(SecureSaveHelper),    // 配置落盘
        typeof(HTTPClientHelper),    // 共享 HttpClient（Universalis）
        typeof(NotifyHelper),        // 提示弹窗
    ];

    private static readonly HashSet<Type> EnabledServiceTypeSet = [.. ENABLED_SERVICE_TYPES];

    /// <summary>本次会话实际实例化并 <c>Init</c> 的服务类型（供插件加载自检输出）。</summary>
    public static IReadOnlyList<Type> InitializedServiceTypes =>
        InternalInstance?.initializedServiceOrder ?? [];

    /// <summary>库内存在但**未被白名单启用**的服务类型（正常为空；非空即说明库内新增了服务文件）。</summary>
    public static IReadOnlyList<Type> SkippedServiceTypes { get; private set; } = [];

    private List<Type> DiscoverEnabledServiceTypes()
    {
        var allServiceTypes = Assembly.GetExecutingAssembly()
                                      .GetTypes()
                                      .Where(t => typeof(OmenServiceBase).IsAssignableFrom(t) && !t.IsAbstract)
                                      .ToList();

        var skipped = allServiceTypes.Where(t => !EnabledServiceTypeSet.Contains(t)).ToList();

        if (skipped.Count > 0)
        {
            SkippedServiceTypes = skipped;

            // 常开一行：库内出现了白名单之外的服务 → 不实例化、不 Init（因此不挂 Hook、不起后台任务）。
            DLog.Warning
            (
                $"[OmenTools][服务白名单] 已跳过 {skipped.Count} 个未启用服务（不会实例化）："
                + string.Join(", ", skipped.Select(static x => x.Name))
            );
        }

        return [.. ENABLED_SERVICE_TYPES.Where(t => !InitOptions.IsDisabled(t))];
    }

    private void InstantiateServices(IEnumerable<Type> serviceTypes)
    {
        foreach (var serviceType in serviceTypes)
        {
            if (Activator.CreateInstance(serviceType) is not OmenServiceBase serviceInstance)
                throw new InvalidOperationException($"初始化 OmenService 错误: {serviceType.FullName}");

            OmenServices.TryAdd(serviceType, serviceInstance);
        }
    }

    private void UninitOmenServices()
    {
        foreach (var serviceType in initializedServiceOrder.AsEnumerable().Reverse())
        {
            if (OmenServices.TryGetValue(serviceType, out var service))
                service.PublicUninit();
        }
    }

    private void ResetServiceState()
    {
        OmenServices  = [];
        TaskHelpers   = [];
        MemoryPatches = [];
        Hooks         = [];

        initializedServiceOrder = [];
        InitOptions             = new();
    }

    internal void RegTaskHelper(TaskHelper taskHelper)
    {
        ArgumentNullException.ThrowIfNull(taskHelper);
        TaskHelpers.TryAdd(taskHelper, 0);
    }

    internal void UnregTaskHelper(TaskHelper taskHelper)
    {
        ArgumentNullException.ThrowIfNull(taskHelper);
        TaskHelpers.TryRemove(taskHelper, out _);
    }

    internal void RegMemoryPatch(MemoryPatch memoryPatch)
    {
        ArgumentNullException.ThrowIfNull(memoryPatch);
        MemoryPatches.TryAdd(memoryPatch, 0);
    }

    internal void UnregMemoryPatch(MemoryPatch memoryPatch)
    {
        ArgumentNullException.ThrowIfNull(memoryPatch);
        MemoryPatches.TryRemove(memoryPatch, out _);
    }

    internal void RegHook(IDalamudHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        Hooks.TryAdd(hook, 0);
    }

    private void DisposeTrackedTaskHelpers()
    {
        foreach (var taskHelper in TaskHelpers.Keys)
        {
            if (taskHelper is not { IsDisposed: false }) continue;
            taskHelper.Dispose();
        }

        TaskHelpers.Clear();
    }

    private void DisposeTrackedMemoryPatches()
    {
        foreach (var memoryPatch in MemoryPatches.Keys)
            memoryPatch.Dispose();

        MemoryPatches.Clear();
    }

    private void DisposeTrackedHooks()
    {
        foreach (var hook in Hooks.Keys)
        {
            if (hook is not { IsDisposed: false }) continue;
            hook.Dispose();
        }

        Hooks.Clear();
    }

    #endregion

    #region Dalamud 服务

    [PluginService]
    public IAddonEventManager AddonEvent { get; private set; } = null!;

    [PluginService]
    public IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    [PluginService]
    public IAgentLifecycle AgentLifecycle { get; private set; } = null!;

    /// <summary>
    ///     尽量不要直接使用，用 FFXIVClientStruct。
    /// </summary>
    [PluginService]
    public IBuddyList BuddyList { get; private set; } = null!;

    /// <summary>
    ///     尽量不要直接使用，大部分功能有其他替代方案。
    /// </summary>
    [PluginService]
    public IChatGui Chat { get; private set; } = null!;

    [PluginService]
    public IClientState ClientState { get; private set; } = null!;

    /// <summary>
    ///     尽量不要直接使用，大部分功能有其他替代方案。
    /// </summary>
    [PluginService]
    public ICommandManager Command { get; private set; } = null!;

    [PluginService]
    public ICondition Condition { get; private set; } = null!;

    /// <summary>
    ///     尽量不要直接使用，大部分功能有其他替代方案。
    /// </summary>
    [PluginService]
    public IContextMenu ContextMenu { get; private set; } = null!;

    /// <summary>
    ///     尽量不要直接使用，大部分功能有其他替代方案。
    /// </summary>
    [PluginService]
    public IDataManager Data { get; private set; } = null!;

    [PluginService]
    public IDtrBar DTRBar { get; private set; } = null!;

    [PluginService]
    public IDutyState DutyState { get; private set; } = null!;

    /// <summary>
    ///     尽量不要直接使用，用 FFXIVClientStruct。
    /// </summary>
    [PluginService]
    public IFateTable Fate { get; private set; } = null!;

    [PluginService]
    public IFlyTextGui FlyText { get; private set; } = null!;

    [PluginService]
    public IFramework Framework { get; private set; } = null!;

    [PluginService]
    public IGameConfig GameConfig { get; private set; } = null!;

    [PluginService]
    public IGameGui GameGUI { get; private set; } = null!;

    [PluginService]
    public IGameInteropProvider Hook { get; private set; } = null!;

    [PluginService]
    public IGameInventory GameInventory { get; private set; } = null!;

    [PluginService]
    public IGameLifecycle GameLifecycle { get; private set; } = null!;

    [PluginService]
    public IGamepadState Gamepad { get; private set; } = null!;

    [PluginService]
    public IJobGauges JobGauges { get; private set; } = null!;

    [PluginService]
    public IKeyState KeyState { get; private set; } = null!;

    [PluginService]
    public IMarketBoard MarketBoard { get; private set; } = null!;

    [PluginService]
    public INamePlateGui NamePlate { get; private set; } = null!;

    [PluginService]
    public INotificationManager DalamudNotification { get; private set; } = null!;

    [PluginService]
    public IPartyFinderGui PartyFinder { get; private set; } = null!;

    [PluginService]
    public IPartyList PartyList { get; private set; } = null!;

    [PluginService]
    public IPlayerState PlayerState { get; private set; } = null!;

    [PluginService]
    public IPluginLog Log { get; private set; } = null!;

    [PluginService]
    public ISeStringEvaluator SeStringEvaluator { get; private set; } = null!;

    [PluginService]
    public ISelfTestRegistry SelfTestRegistry { get; private set; }

    [PluginService]
    public ISigScanner SigScanner { get; private set; }

    [PluginService]
    public ITextureProvider Texture { get; private set; } = null!;

    [PluginService]
    public ITextureReadbackProvider TextureReadback { get; private set; } = null!;

    [PluginService]
    public ITextureSubstitutionProvider TextureSubstitution { get; private set; } = null!;

    [PluginService]
    public ITitleScreenMenu TitleScreenMenu { get; private set; } = null!;

    [PluginService]
    public IToastGui Toast { get; private set; } = null!;

    [PluginService]
    public IUnlockState UnlockState { get; private set; } = null!;

    public IDalamudPluginInterface PI            { get; private set; } = null!;
    public IUiBuilder              UIBuilder     { get; private set; } = null!;
    public IAetheryteList          AetheryteList { get; private set; } = null!;
    public IObjectTable            ObjectTable   { get; private set; } = null!;

    #endregion
}

public interface IOmenDalamudService<T> : IOmenDalamudService where T : IOmenDalamudService<T>
{
    static T Instance() =>
        DService.Instance().GetOmenDalamudService<T>() ??
        throw new InvalidOperationException($"服务 {typeof(T).Name} 尚未注册或初始化");
}

public interface IOmenDalamudService;
