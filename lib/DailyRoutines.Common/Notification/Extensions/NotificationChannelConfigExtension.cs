using DailyRoutines.Common.Notification.Abstractions;

namespace DailyRoutines.Common.Notification.Extensions;

public static class NotificationChannelConfigExtension
{
    extension<T>(T config) where T : NotificationChannelConfig
    {
        public static T? Load(NotificationChannelBase instance) =>
            instance.LoadConfig<T>();

        public void Save(NotificationChannelBase instance) =>
            instance.SaveConfig(config);
    }
}
