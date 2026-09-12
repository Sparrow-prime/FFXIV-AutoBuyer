using System;
using System.IO;
using Newtonsoft.Json;
using OmenTools.Dalamud;
using OmenTools.OmenService;

namespace DailyRoutines.Common.Notification.Abstractions;

public abstract class NotificationChannelBase
{
    public string ConfigFilePath
    {
        get
        {
            var directory = Path.Join(IDalamudPluginInterface.Instance().GetPluginConfigDirectory(), "Notification");
            Directory.CreateDirectory(directory);
            return Path.Join(directory, $"{GetType().Name}.json");
        }
    }
    
    public async Task PublicInitAsync()
    {
        if (IsDisposed || IsInitialized)
            return;

        try
        {
            await Init();
        }
        catch (Exception ex)
        {
            DLog.Error("初始化通知渠道失败", ex);
            await PublicUninitAsync();
        }
        finally
        {
            IsInitialized = true;
        }
    }

    public async Task PublicUninitAsync()
    {
        if (IsDisposed)
            return;

        try
        {
            await Uninit();
        }
        catch (Exception ex)
        {
            DLog.Error("卸载通知渠道失败", ex);
        }
        finally
        {
            IsDisposed = true;
        }
    }
    
    #region 私有控制

    public bool IsDisposed { get; private set; }

    public bool IsInitialized { get; private set; }

    #endregion

    #region 配置

    internal T? LoadConfig<T>() where T : NotificationChannelConfig
    {
        try
        {
            if (!File.Exists(ConfigFilePath)) return null;

            var jsonString = File.ReadAllText(ConfigFilePath);
            return JsonConvert.DeserializeObject<T>(jsonString, JsonSerializerSettings.GetShared());
        }
        catch (Exception ex)
        {
            DLog.Error($"为通知渠道加载配置失败: {GetType().Name}", ex);
            return null;
        }
    }

    internal void SaveConfig<T>(T config) where T : NotificationChannelConfig
    {
        try
        {
            ArgumentNullException.ThrowIfNull(config);
            
            var jsonString = JsonConvert.SerializeObject(config, Formatting.Indented, JsonSerializerSettings.GetShared());
            SecureSaveHelper.Instance().WriteAllText(ConfigFilePath, jsonString);
        }
        catch (Exception ex)
        {
            DLog.Error($"为通知渠道加载配置失败: {GetType().Name}", ex);
        }
    }

    #endregion
    
    #region 继承
    
    protected static string CacheDirectory
    {
        get
        {
            if (!string.IsNullOrEmpty(field))
                return field;
            
            var path = Path.Join(IDalamudPluginInterface.Instance().GetPluginConfigDirectory(), "Notification", "Cache");
            Directory.CreateDirectory(path);
            return field = path;
        }
    }

    protected virtual Task Init() => Task.CompletedTask;
    
    protected virtual Task Uninit() => Task.CompletedTask;
    
    public virtual void ConfigUI() { }

    #endregion
}
