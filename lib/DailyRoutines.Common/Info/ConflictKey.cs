using System.Linq;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Keys;

namespace DailyRoutines.Common.Info;

public class ConflictKey
{
    public VirtualKey Keyboard
    {
        get;
        set => field = IKeyState.Instance().GetValidVirtualKeys().Contains(field) ? value : VirtualKey.SHIFT;
    } = VirtualKey.SHIFT;

    public GamepadButtons Gamepad  { get; set; } = GamepadButtons.South | GamepadButtons.West;
    
    // ReSharper disable once CompareOfFloatsByEqualityOperator
    public bool IsPressed() =>
        IKeyState.Instance()[Keyboard] ||
        IGamepadState.Instance().Raw(Gamepad) == 1;

    public override string ToString() =>
        $"{Keyboard} | {Gamepad}";
}
