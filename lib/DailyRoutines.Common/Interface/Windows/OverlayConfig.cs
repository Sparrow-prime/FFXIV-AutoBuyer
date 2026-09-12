using DailyRoutines.Common.Module.Abstractions;
using DailyRoutines.Common.Runtime.Hosts;
using Dalamud.Interface.Windowing;

namespace DailyRoutines.Common.Interface.Windows;

public class OverlayConfig : Window
{
    private const ImGuiWindowFlags WINDOW_FLAGS = ImGuiWindowFlags.NoScrollbar;

    public OverlayConfig(ModuleBase moduleBase) :
        base($"{moduleBase.Info.Title}###{moduleBase.GetType().Name}ConfigOverlay")
    {
        Flags              = WINDOW_FLAGS;
        RespectCloseHotkey = false;
        ModuleBase         = moduleBase;

        ManagerHost.Current.AddWindow(this);
    }

    private ModuleBase ModuleBase { get; init; }

    public override void Draw() => ModuleBase.PublicConfigUI();

    public override bool DrawConditions() => ModuleBase.IsEnabled;
}
