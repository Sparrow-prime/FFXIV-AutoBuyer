using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using DailyRoutines.Common.Interface.Windows;
using DailyRoutines.Common.Module.Enums;
using DailyRoutines.Common.Module.Models;
using DailyRoutines.Common.Runtime.Hosts;
using Dalamud.Hooking;
using Newtonsoft.Json;
using OmenTools.Dalamud;
using OmenTools.Interop.Game;
using OmenTools.OmenService;
using OmenTools.Threading.TaskHelper;

namespace DailyRoutines.Common.Module.Abstractions;

public abstract class ModuleBase : IEquatable<ModuleBase>
{
    protected ModuleBase()
    {
        ModuleGUID = Guid.Empty.ToString();
        ModuleName = GetType().Name;
        WithConfig = File.Exists(ConfigFilePath);

        WithConfigUI = GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                                .Any(m => m.Name == nameof(ConfigUI) && m.DeclaringType != typeof(ModuleBase));
    }

    #region 暴露信息

    public string           ModuleName       { get; private set; }
    public List<ModuleBase> PrecedingModules { get; set; }
    public List<ModuleBase> RecommendModules { get; set; }
    public List<ModuleBase> ConflictModules  { get; set; }
    public bool             WithConfigUI     { get; private set; }
    public bool             WithConfig       { get; private set; }
    public string           ModuleIdentifier { get; set; }
    public string           ModuleGUID       { get; init; }

    #endregion

    #region 模块控制

    public void Load(bool affectConfig = false) =>
        _ = LoadAsync(affectConfig);

    public Task LoadAsync(bool affectConfig = false) =>
        ManagerHost.Current.LoadAsync(this, affectConfig);

    public void Unload(bool affectConfig = false) =>
        _ = UnloadAsync(affectConfig);

    public Task UnloadAsync(bool affectConfig = false) =>
        ManagerHost.Current.UnloadAsync(this, affectConfig);

    public void ToggleOverlayConfig(bool? isEnabled = null)
    {
        if (!WithConfigUI) return;
        OverlayConfig ??= new(this);

        isEnabled            ??= !OverlayConfig.IsOpen;
        OverlayConfig.IsOpen =   (bool)isEnabled;
    }

    #endregion

    #region 覆写

    public override string ToString() => ModuleIdentifier;

    public bool Equals(ModuleBase? other)
    {
        if (other is null) return false;
        return ModuleGUID == other.ModuleGUID;
    }

    public override bool Equals(object? obj) => Equals(obj as ModuleBase);

    public override int GetHashCode() => ModuleGUID.GetHashCode();

    #endregion

    #region 虚拟方法包装暴露

    public void PublicInit()
    {
        if (IsEnabled)
            return;

        try
        {
            IsDisposed = false;

            BaseInit();
            Init();

            IsInitialized = true;
        }
        catch (Exception ex)
        {
            DLog.Error("初始化模块时发生错误", ex);
            PublicUninit();
        }
    }

    public void PublicUninit()
    {
        if (IsDisposed)
            return;

        try
        {
            try
            {
                Uninit();
            }
            catch (Exception ex)
            {
                DLog.Error("卸载模块时发生错误", ex);
            }

            try
            {
                BaseUninit();
            }
            catch (Exception ex)
            {
                DLog.Error("基础卸载模块时发生错误", ex);
            }
        }
        finally
        {
            IsDisposed = true;
        }
    }

    public void PublicConfigUI() => ConfigUI();

    public void PublicOverlayUI() => OverlayUI();

    public void PublicOverlayOnOpen() => OverlayOnOpen();

    public void PublicOverlayOnClose() => OverlayOnClose();

    public void PublicOverlayPreDraw() => OverlayPreDraw();

    public void PublicOverlayPostDraw() => OverlayPostDraw();

    public void PublicOverlayUpdate() => OverlayUpdate();

    public void PublicOverlayPreOpenCheck() => OverlayPreOpenCheck();

    #endregion

    #region 继承

    public virtual ModuleInfo Info { get; } = new()
    {
        Title       = ManagerHost.Current.GetLoc("DevModuleTitle"),
        Description = ManagerHost.Current.GetLoc("DevModuleDescription"),
        Category    = ModuleCategory.General
    };

    public virtual ModulePermission Permission { get; } = new();

    protected virtual void Init() { }

    protected virtual void ConfigUI() { }

    protected virtual void OverlayUI() { }

    protected virtual void OverlayOnOpen() { }

    protected virtual void OverlayOnClose() { }

    protected virtual void OverlayPreDraw() { }

    protected virtual void OverlayPostDraw() { }

    protected virtual void OverlayUpdate() { }

    protected virtual void OverlayPreOpenCheck() { }

    protected virtual void Uninit() { }

    #endregion

    #region 生命周期

    public bool IsInitialized { get; private set; }

    public bool IsDisposed { get; private set; }

    public bool IsEnabled => IsInitialized && !IsDisposed;

    private void BaseInit()
    {
        CleanupDelegates.GetOrAdd(GetType(), GenerateCleanupDelegate);
        IPCAttributeRegistry.RegObjectIPCs(this);
    }

    private void BaseUninit()
    {
        if (Overlay is not null)
            ManagerHost.Current.RemoveWindow(Overlay);
        Overlay = null;

        if (OverlayConfig is not null)
            ManagerHost.Current.RemoveWindow(OverlayConfig);
        OverlayConfig = null;

        IPCAttributeRegistry.UnregObjectIPCs(this);

        TaskHelper?.Abort();
        TaskHelper?.Dispose();
        TaskHelper = null;

        if (CleanupDelegates.TryGetValue(GetType(), out var action))
            action?.Invoke(this);
    }

    #endregion

    #region 配置

    public string ConfigFilePath =>
        Path.Join(IDalamudPluginInterface.Instance().GetPluginConfigDirectory(), $"{ModuleName}.json");

    public string ConfigDirectoryPath
    {
        get
        {
            var path = Path.Join(IDalamudPluginInterface.Instance().GetPluginConfigDirectory(), "Module", $"{ModuleName}");

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }
    }

    public T? LoadConfig<T>() where T : ModuleConfig
    {
        try
        {
            T? config = null;

            if (File.Exists(ConfigFilePath))
            {
                var jsonString = File.ReadAllText(ConfigFilePath);
                config = JsonConvert.DeserializeObject<T>(jsonString, JsonSerializerSettings.GetShared());
            }

            if (config == null)
            {
                var tempConfig     = Activator.CreateInstance<T>();
                var prevModuleName = tempConfig?.PreviousModuleName;

                if (!string.IsNullOrWhiteSpace(prevModuleName))
                {
                    var prevConfigFilePath = Path.Join(IDalamudPluginInterface.Instance().GetPluginConfigDirectory(), $"{prevModuleName}.json");

                    if (File.Exists(prevConfigFilePath))
                    {
                        var prevJSONString = File.ReadAllText(prevConfigFilePath);
                        config = JsonConvert.DeserializeObject<T>(prevJSONString, JsonSerializerSettings.GetShared());
                        if (config != null)
                            SaveConfig(config);
                    }
                }
            }

            return config;
        }
        catch (Exception ex)
        {
            DLog.Error($"加载模块配置失败: {GetType().Name}", ex);
            return null;
        }
    }

    public void SaveConfig<T>(T config) where T : ModuleConfig
    {
        try
        {
            ArgumentNullException.ThrowIfNull(config);

            var jsonString = JsonConvert.SerializeObject(config, Formatting.Indented, JsonSerializerSettings.GetShared());
            SecureSaveHelper.Instance().WriteAllText(ConfigFilePath, jsonString);
        }
        catch (Exception ex)
        {
            DLog.Error($"加载模块配置失败: {GetType().Name}", ex);
        }
    }

    protected static void ExportToClipboard<T>(T config) where T : class
    {
        var host = ManagerHost.Current;

        try
        {
            ArgumentNullException.ThrowIfNull(config);

            ImGui.SetClipboardText(config.ToJSONBase64());
            NotifyHelper.Instance().NotificationSuccess(host.GetLoc("DailyModuleBase-Exported"));
        }
        catch (Exception ex)
        {
            var errorText = host.GetLoc("DailyModuleBase-ExportError");
            DLog.Error(errorText, ex);
            NotifyHelper.Instance().NotificationError(errorText);
        }
    }

    protected static T? ImportFromClipboard<T>() where T : class
    {
        var host = ManagerHost.Current;

        try
        {
            var config = ImGui.GetClipboardText().FromJSONBase64<T>();

            if (config != null)
                NotifyHelper.Instance().NotificationSuccess(host.GetLoc("DailyModuleBase-Exported"));

            return config;
        }
        catch (Exception ex)
        {
            var errorText = host.GetLoc("DailyModuleBase-ImportError");
            DLog.Error(errorText, ex);
            NotifyHelper.Instance().NotificationError(errorText);
        }

        return null;
    }

    #endregion

    #region 保护

    protected TaskHelper?    TaskHelper    { get; set; }
    protected Overlay?       Overlay       { get; set; }
    protected OverlayConfig? OverlayConfig { get; set; }

    #endregion

    #region 私有

    private static readonly ConcurrentDictionary<Type, Action<ModuleBase>?> CleanupDelegates = [];

    private static Action<ModuleBase>? GenerateCleanupDelegate(Type type)
    {
        const BindingFlags FLAGS_ALL = BindingFlags.Instance  |
                                       BindingFlags.NonPublic |
                                       BindingFlags.Public    |
                                       BindingFlags.Static;

        var fields = type.GetFields(FLAGS_ALL)
                         .Where
                         (f => (f.FieldType.IsGenericType &&
                                f.FieldType.GetGenericTypeDefinition() == typeof(Hook<>)) ||
                               typeof(MemoryPatch).IsAssignableFrom(f.FieldType)
                         )
                         .ToList();

        if (fields.Count == 0) return null;

        var instanceParam     = Expression.Parameter(typeof(ModuleBase), "module");
        var convertedInstance = Expression.Convert(instanceParam, type);
        var bodyExpressions   = new List<Expression>();

        foreach (var field in fields)
        {
            var fieldExp  = Expression.Field(field.IsStatic ? null : convertedInstance, field);
            var isNotNull = Expression.NotEqual(fieldExp, Expression.Constant(null));

            var disposeCall = Expression.Call
            (
                Expression.Convert(fieldExp, typeof(IDisposable)),
                typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose))!
            );

            Expression cleanup;

            if (!field.IsInitOnly)
            {
                var setNull = Expression.Assign(fieldExp, Expression.Convert(Expression.Constant(null), field.FieldType));
                cleanup = Expression.Block(disposeCall, setNull);
            }
            else
                cleanup = disposeCall;

            bodyExpressions.Add(Expression.IfThen(isNotNull, cleanup));
        }

        return Expression.Lambda<Action<ModuleBase>>(Expression.Block(bodyExpressions), instanceParam).Compile();
    }

    #endregion
}
