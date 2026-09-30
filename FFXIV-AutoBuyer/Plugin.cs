using Dalamud.Plugin;
using FFXIVAutoBuyer.Host;
using FFXIVAutoBuyer.Localization;
using FFXIVAutoBuyer.MarketBoard;
using OmenTools;
using OmenTools.Dalamud;
using OmenTools.OmenService;

namespace FFXIVAutoBuyer;

/// <summary>
/// 插件入口：初始化 OmenTools 服务容器 → 本地化 → 宿主 → 模块。
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;

    public MarketBoardModule Module { get; }

    public Plugin
    (
        IDalamudPluginInterface pluginInterface
    )
    {
        this.pluginInterface = pluginInterface;

        // 本插件不使用道具 / 技能工具提示上的市场数据：TooltipManager 已在第五十五轮**从内嵌库中删除**
        // （它 Init 时会无条件挂上游戏 ItemDetail / ActionDetail 的监听，于是「每悬停一次道具」
        //   都会打两条冗长日志、并让所有提示框都走一遍它的流程）。
        // 现在库内**只有** DService.ENABLED_SERVICE_TYPES 白名单里的服务会被实例化；白名单外的服务
        // 一律不实例化（不挂 Hook、不起 tick、不发请求），见 lib/FROZEN.md。
        DService.Init(pluginInterface);

        // 模块的 ModuleInfo 会在构造时调用 Lang.Get，因此本地化必须先于模块实例化完成
        LocalizationSetup.Configure(pluginInterface);

        PluginHost.Register();

        Module = new();
        Module.PublicInit();

        LogStartupSelfCheck();

        // 插件安装器 / 插件列表中「打开主界面」「设置」按钮的入口
        pluginInterface.UiBuilder.OpenMainUi   += OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
    }

    /// <summary>
    /// 加载自检（常开，`/xllog` 过滤 <c>[AutoBuyer][自检]</c>）。
    /// <para>
    /// 目的：一旦游戏里出现异常行为，能第一时间判断它是不是**本插件**做的 ——
    /// 本插件只启用白名单内的服务、只注册自己的命令与 IPC、只访问 universalis.app，
    /// 且写入游戏市场状态的位置只有一处（`RequestLocalSearchData`）。
    /// 游戏内可以同时装着 DR 一类 OmenTools 系插件，但它们在**各自的程序集**里运行各自的副本，
    /// 与本插件这份冻结副本互不影响；因此「本插件没做」就意味着是别的东西做的。
    /// </para>
    /// </summary>
    private static void LogStartupSelfCheck()
    {
        var services = DService.InitializedServiceTypes;
        var skipped  = DService.SkippedServiceTypes;

        DLog.Warning
        (
            $"[AutoBuyer][自检] 已启用服务 {services.Count} 个（白名单）："
            + string.Join("、", services.Select(static x => x.Name))
            + (skipped.Count > 0 ?
                   $"；另有 {skipped.Count} 个白名单外服务已跳过（不会实例化）" :
                   "；白名单外无其它服务文件")
        );

        DLog.Warning
        (
            "[AutoBuyer][自检] 本方公共面：命令=/market；"
            + "IPC=FFXIVAutoBuyer.MarketBoard.{SearchItem,ToggleOverlay}；"
            + "出网=universalis.app（仅布告板窗口打开时）；"
            + "无 Addon/右键菜单/Tooltip/封包/输入 Hook"
        );
    }

    private void OpenMainUi() =>
        Module.ToggleOverlayPublic(true);

    private void OpenConfigUi() =>
        Module.ToggleConfigPublic(true);

    public void Dispose()
    {
        pluginInterface.UiBuilder.OpenMainUi   -= OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;

        Module.PublicUninit();
        DService.Uninit();
    }
}
