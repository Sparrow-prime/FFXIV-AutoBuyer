using Dalamud.Plugin;
using FFXIVAutoBuyer.Host;
using FFXIVAutoBuyer.Localization;
using FFXIVAutoBuyer.MarketBoard;
using OmenTools;
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

        // 本插件不使用道具 / 技能工具提示上的市场数据，因此**不初始化** TooltipManager：
        // 它的 Init 会无条件挂上游戏 ItemDetail / ActionDetail 的 PreRequestedUpdate 监听，
        // 于是「每悬停一次道具」都会打两条冗长日志、并让所有提示框都走一遍它的流程。
        // 关掉后：日志干净（不再出现 [TooltipManager] 行）、提示框不被追加任何内容。
        DService.Init(pluginInterface, static () => new DServiceInitOptions().Disable<TooltipManager>());

        // 模块的 ModuleInfo 会在构造时调用 Lang.Get，因此本地化必须先于模块实例化完成
        LocalizationSetup.Configure(pluginInterface);

        PluginHost.Register();

        Module = new();
        Module.PublicInit();

        // 插件安装器 / 插件列表中「打开主界面」「设置」按钮的入口
        pluginInterface.UiBuilder.OpenMainUi   += OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
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
