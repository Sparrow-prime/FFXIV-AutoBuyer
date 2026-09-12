using System.Collections.Concurrent;
using Newtonsoft.Json;
using OmenTools.Dalamud;
using OmenTools.OmenService;
using OmenTools.Threading;

namespace DailyRoutines.Common.DataUploader.Abstractions;

public abstract class DataUploaderBase
{
    private readonly CancellationTokenSource          cancellationTokenSource = new();
    private readonly ConcurrentDictionary<long, Task> backgroundTasks         = [];
    private readonly TaskCompletionSource             stoppedCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int  lifecycleState;
    private int  activeBackgroundRegistrations;
    private long nextBackgroundTaskID;

    public string ConfigFilePath
    {
        get
        {
            var directory = Path.Join(IDalamudPluginInterface.Instance().GetPluginConfigDirectory(), "DataUploader");
            Directory.CreateDirectory(directory);
            return Path.Join(directory, $"{GetType().Name}.json");
        }
    }
    
    public async Task PublicInitAsync()
    {
        if (Interlocked.CompareExchange(ref lifecycleState, STATE_RUNNING, STATE_CREATED) != STATE_CREATED)
            return;

        try
        {
            await Init();
            IsInitialized = true;
        }
        catch (Exception ex)
        {
            DLog.Error("初始化数据上传器失败", ex);
            await PublicUninitAsync();
        }
    }

    public async Task PublicUninitAsync()
    {
        if (!TryBeginStop())
        {
            await stoppedCompletionSource.Task;
            return;
        }

        try
        {
            try
            {
                await Stop();
            }
            catch (Exception ex)
            {
                DLog.Error("停止数据上传器失败", ex);
            }

            await DrainBackgroundTasksAsync();

            try
            {
                await Uninit();
            }
            catch (Exception ex)
            {
                DLog.Error("卸载数据上传器失败", ex);
            }
        }
        finally
        {
            cancellationTokenSource.Dispose();
            Interlocked.Exchange(ref lifecycleState, STATE_STOPPED);
            stoppedCompletionSource.TrySetResult();
        }
    }

    #region 生命周期控制

    public bool IsDisposed => Volatile.Read(ref lifecycleState) == STATE_STOPPED;

    public bool IsStopping => Volatile.Read(ref lifecycleState) == STATE_STOPPING;

    public bool IsInitialized { get; private set; }

    #endregion

    #region 继承
    
    protected virtual Task Init() => Task.CompletedTask;

    protected virtual Task Stop() => Task.CompletedTask;

    protected virtual Task Uninit() => Task.CompletedTask;

    protected bool RunBackground(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (Volatile.Read(ref lifecycleState) != STATE_RUNNING)
            return false;

        Interlocked.Increment(ref activeBackgroundRegistrations);

        try
        {
            if (Volatile.Read(ref lifecycleState) != STATE_RUNNING)
                return false;

            var taskID = Interlocked.Increment(ref nextBackgroundTaskID);
            var task   = Task.Run(() => ExecuteBackgroundTaskAsync(operation), CancellationToken.None);

            backgroundTasks[taskID] = task;
            _ = task.ContinueWith
            (
                completedTask => backgroundTasks.TryRemove(taskID, out _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );

            return true;
        }
        finally
        {
            Interlocked.Decrement(ref activeBackgroundRegistrations);
        }
    }

    private bool TryBeginStop()
    {
        while (true)
        {
            var currentState = Volatile.Read(ref lifecycleState);
            if (currentState is STATE_STOPPING or STATE_STOPPED)
                return false;

            if (Interlocked.CompareExchange(ref lifecycleState, STATE_STOPPING, currentState) == currentState)
                return true;
        }
    }

    private async Task DrainBackgroundTasksAsync()
    {
        try
        {
            await cancellationTokenSource.CancelAsync();
        }
        catch
        {
            // ignored
        }

        while (Volatile.Read(ref activeBackgroundRegistrations) != 0)
            await Task.Yield();

        while (!backgroundTasks.IsEmpty)
            await Task.WhenAll(backgroundTasks.Values);
    }

    private async Task ExecuteBackgroundTaskAsync(Func<CancellationToken, Task> operation)
    {
        try
        {
            await operation(cancellationTokenSource.Token);
        }
        catch
        {
            // ignored
        }
    }

    #endregion

    #region 配置

    internal T? LoadConfig<T>() where T : DataUploaderConfig
    {
        try
        {
            if (!File.Exists(ConfigFilePath)) return null;

            var jsonString = File.ReadAllText(ConfigFilePath);
            return JsonConvert.DeserializeObject<T>(jsonString, JsonSerializerSettings.GetShared());
        }
        catch (Exception ex)
        {
            DLog.Error($"为数据上传器加载配置失败: {GetType().Name}", ex);
            return null;
        }
    }

    internal void SaveConfig<T>(T config) where T : DataUploaderConfig
    {
        try
        {
            ArgumentNullException.ThrowIfNull(config);
            
            var jsonString = JsonConvert.SerializeObject(config, Formatting.Indented, JsonSerializerSettings.GetShared());
            SecureSaveHelper.Instance().WriteAllText(ConfigFilePath, jsonString);
        }
        catch (Exception ex)
        {
            DLog.Error($"为数据上传器加载配置失败: {GetType().Name}", ex);
        }
    }

    #endregion
    
    protected static readonly Throttler<string> Throttler = new();
    
    protected static bool IsPlayerReady() =>
        GameState.IsLoggedIn                                &&
        IObjectTable.Instance().LocalPlayer != null &&
        !ICondition.Instance().IsBoundByDuty;

    #region 常量

    private const int STATE_CREATED  = 0;
    private const int STATE_RUNNING  = 1;
    private const int STATE_STOPPING = 2;
    private const int STATE_STOPPED  = 3;

    #endregion
}
