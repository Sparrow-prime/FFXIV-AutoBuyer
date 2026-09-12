using DailyRoutines.Common.DataUploader.Abstractions;

namespace DailyRoutines.Common.DataUploader.Extensions;

public static class DataUploaderConfigExtension
{
    extension<T>(T config) where T : DataUploaderConfig
    {
        public static T? Load(DataUploaderBase instance) =>
            instance.LoadConfig<T>();

        public void Save(DataUploaderBase instance) =>
            instance.SaveConfig(config);
    }
}
