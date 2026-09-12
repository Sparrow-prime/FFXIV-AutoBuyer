using DailyRoutines.Common.Manager.Abstractions;

namespace DailyRoutines.Common.Manager.Extensions;

public static class ManagerConfigExtension
{
    extension<T>(T config) where T : ManagerConfig
    {
        public static T? Load(ManagerBase instance) =>
            instance.LoadConfig<T>();

        public void Save(ManagerBase instance) =>
            instance.SaveConfig(config);
    }
}
