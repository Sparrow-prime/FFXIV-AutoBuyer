using DailyRoutines.Common.Module.Abstractions;
using DailyRoutines.Common.Runtime.Hosts;
using Dalamud.Interface.Windowing;

namespace DailyRoutines.Common.Interface.Windows;

public class Overlay : Window
{
    private const ImGuiWindowFlags WINDOW_FLAGS =
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar;

    public Overlay(ModuleBase moduleBase, string? title = null) :
        base($"{(string.IsNullOrEmpty(title) ? string.Empty : title)}###{moduleBase.GetType().FullName ?? moduleBase.ModuleName}")
    {
        Flags              = WINDOW_FLAGS;
        RespectCloseHotkey = false;
        ModuleBase         = moduleBase;

        ManagerHost.Current.AddWindow(this);
    }

    private ModuleBase ModuleBase { get; init; }

    public override void Draw() => ModuleBase.PublicOverlayUI();

    public override void OnOpen() => ModuleBase.PublicOverlayOnOpen();

    public override void OnClose() => ModuleBase.PublicOverlayOnClose();

    public override void PreDraw() => ModuleBase.PublicOverlayPreDraw();

    public override void PostDraw() => ModuleBase.PublicOverlayPostDraw();

    public override void Update() => ModuleBase.PublicOverlayUpdate();

    public override void PreOpenCheck() => ModuleBase.PublicOverlayPreOpenCheck();

    public override bool DrawConditions() => ModuleBase.IsEnabled;
}
