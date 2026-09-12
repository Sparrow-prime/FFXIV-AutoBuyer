using System.Collections.Concurrent;
using DailyRoutines.Common.Runtime.Hosts;
using Newtonsoft.Json;
using OmenTools.Dalamud;
using OmenTools.OmenService;

namespace DailyRoutines.Common.Manager.Abstractions;

public abstract class ManagerBase<T> : ManagerBase where T : ManagerBase<T>
{
    public static T Instance() =>
        ManagerHost.Current.Get<T>() is { IsInitialized: true, IsStopping: false, IsDisposed: false } manager ?
            manager :
            throw new InvalidOperationException($"管理器 {typeof(T).Name} 尚未注册或初始化");
}

public abstract class ManagerBase
{
    private readonly CancellationTokenSource          cancellationTokenSource = new();
    private readonly ConcurrentDictionary<long, Task> backgroundTasks         = [];
    private readonly AsyncLocal<long?>                currentBackgroundTaskID = new();
    private readonly TaskCompletionSource             stoppedCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int  lifecycleState;
    private int  activeBackgroundRegistrations;
    private int  isCancellationTokenSourceDisposed;
    private long nextBackgroundTaskID;

    public string ConfigFilePath
    {
        get
        {
            var directory = Path.Join(IDalamudPluginInterface.Instance().GetPluginConfigDirectory(), "Manager");
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
            DLog.Error($"在初始化管理器时发生错误: {GetType().Name}", ex);
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
                DLog.Error($"在停止管理器时发生错误: {GetType().Name}", ex);
            }

            await DrainBackgroundTasksAsync();

            try
            {
                await Uninit();
            }
            catch (Exception ex)
            {
                DLog.Error($"在卸载管理器时发生错误: {GetType().Name}", ex);
            }
        }
        finally
        {
            Interlocked.Exchange(ref lifecycleState, STATE_STOPPED);
            stoppedCompletionSource.TrySetResult();
            TryDisposeCancellationTokenSource();
        }
    }

    public async Task PublicPostInitAsync()
    {
        if (Volatile.Read(ref lifecycleState) != STATE_RUNNING)
            return;

        try
        {
            await PostInit();
        }
        catch (Exception ex)
        {
            DLog.Error($"在管理器后初始化时发生错误: {GetType().Name}", ex);
        }
    }

    #region 生命周期

    public bool IsInitialized { get; private set; }

    public bool IsDisposed => Volatile.Read(ref lifecycleState) == STATE_STOPPED;

    public bool IsStopping => Volatile.Read(ref lifecycleState) == STATE_STOPPING;

    #endregion

    #region 继承

    protected virtual Task Init() => Task.CompletedTask;

    protected virtual Task PostInit() => Task.CompletedTask;

    protected virtual Task Stop() => Task.CompletedTask;

    protected virtual Task Uninit() => Task.CompletedTask;

    protected bool RunBackground
    (
        Func<CancellationToken, Task> operation
    )
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
            var task   = Task.Run(() => ExecuteBackgroundTaskAsync(taskID, operation), CancellationToken.None);

            backgroundTasks[taskID] = task;
            _ = task.ContinueWith
            (
                completedTask =>
                {
                    backgroundTasks.TryRemove(taskID, out _);
                    TryDisposeCancellationTokenSource();
                },
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

    #endregion

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

        var currentTaskID = currentBackgroundTaskID.Value;

        while (true)
        {
            var pendingTasks = backgroundTasks.Where(pair => pair.Key != currentTaskID)
                                              .Select(pair => pair.Value)
                                              .ToArray();
            if (pendingTasks.Length == 0)
                return;

            await Task.WhenAll(pendingTasks);
        }
    }

    private async Task ExecuteBackgroundTaskAsync
    (
        long                          taskID,
        Func<CancellationToken, Task> operation
    )
    {
        var previousTaskID = currentBackgroundTaskID.Value;
        currentBackgroundTaskID.Value = taskID;

        try
        {
            await operation(cancellationTokenSource.Token);
        }
        catch
        {
            // ignored
        }
        finally
        {
            currentBackgroundTaskID.Value = previousTaskID;
        }
    }

    private void TryDisposeCancellationTokenSource()
    {
        if (Volatile.Read(ref lifecycleState)                != STATE_STOPPED ||
            Volatile.Read(ref activeBackgroundRegistrations) != 0             ||
            !backgroundTasks.IsEmpty)
            return;

        if (Interlocked.Exchange(ref isCancellationTokenSourceDisposed, 1) == 0)
            cancellationTokenSource.Dispose();
    }

    #region 配置

    internal T? LoadConfig<T>() where T : ManagerConfig
    {
        try
        {
            if (!File.Exists(ConfigFilePath)) return null;

            var jsonString = File.ReadAllText(ConfigFilePath);
            return JsonConvert.DeserializeObject<T>(jsonString, JsonSerializerSettings.GetShared());
        }
        catch (Exception ex)
        {
            DLog.Error($"为管理器加载配置失败: {GetType().Name}", ex);
            return null;
        }
    }

    internal void SaveConfig<T>
    (
        T config
    ) where T : ManagerConfig
    {
        try
        {
            ArgumentNullException.ThrowIfNull(config);

            var jsonString = JsonConvert.SerializeObject(config, Formatting.Indented, JsonSerializerSettings.GetShared());
            SecureSaveHelper.Instance().WriteAllText(ConfigFilePath, jsonString);
        }
        catch (Exception ex)
        {
            DLog.Error($"为管理器保存配置失败: {GetType().Name}", ex);
        }
    }

    #endregion

    #region 常量

    private const int STATE_CREATED  = 0;
    private const int STATE_RUNNING  = 1;
    private const int STATE_STOPPING = 2;
    private const int STATE_STOPPED  = 3;

    #endregion
}
