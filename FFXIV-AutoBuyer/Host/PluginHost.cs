using DailyRoutines.Common.Manager.Abstractions;
using DailyRoutines.Common.Module.Abstractions;
using DailyRoutines.Common.Runtime.Abstractions;
using DailyRoutines.Common.Runtime.Hosts;
using Dalamud.Interface.Windowing;
using FFXIVAutoBuyer.Manager;
using OmenTools.OmenService;

namespace FFXIVAutoBuyer.Host;

/// <summary>
/// 为内嵌的 <c>DailyRoutines.Common</c> 提供宿主能力（本插件为单模块插件）。
/// </summary>
internal sealed class PluginHost : IManagerHost
{
    public static void Register() =>
        ManagerHost.Current = new PluginHost();

    /// <summary>本插件没有 DR 意义上的 Manager，固定返回 null。</summary>
    public T? Get<T>() where T : ManagerBase =>
        null;

    public Task LoadAsync
    (
        ModuleBase module,
        bool       affectConfig
    )
    {
        module.PublicInit();
        return Task.CompletedTask;
    }

    public Task UnloadAsync
    (
        ModuleBase module,
        bool       affectConfig
    )
    {
        module.PublicUninit();
        return Task.CompletedTask;
    }

    public bool AddWindow
    (
        Window window
    ) =>
        WindowManager.Instance().AddWindow(window);

    public bool RemoveWindow
    (
        Window? window
    ) =>
        WindowManager.Instance().RemoveWindow(window);

    public string GetLoc
    (
        string   key,
        params object[] args
    ) =>
        LanguageManager.Get(key, args);
}
