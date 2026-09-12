using DailyRoutines.Common.Runtime.Abstractions;

namespace DailyRoutines.Common.Runtime.Hosts;

public static class ManagerHost
{
    public static IManagerHost Current
    {
        get => field ?? throw new InvalidOperationException("管理器能力提供方宿主尚未注册");
        set;
    }
}
