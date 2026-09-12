using System.Collections.Concurrent;
using DailyRoutines.Common.RemoteInteraction.Enums;
using DailyRoutines.Common.RemoteInteraction.Models;
using OmenTools.Dalamud;

namespace FFXIVAutoBuyer.Universalis;

/// <summary>
/// 带 TTL 缓存与观察者机制的远程数据查询槽。
/// 同一 key 的并发请求会被合并；失败时按 <c>failureTtl</c> 退避，避免打爆上游 API。
/// </summary>
/// <typeparam name="TValue">远程数据类型。</typeparam>
internal sealed class RemoteQueryCache<TValue> where TValue : class
{
    private sealed class Entry
    {
        public RemoteSnapshot<TValue> Snapshot = new(RemoteSnapshotStatus.Empty, null, default, null);

        public DateTime ExpiresAtUtc = DateTime.MinValue;

        /// <summary>连续失败次数（用于指数退避）。</summary>
        public int FailureCount;

        /// <summary>失败退避截止时间：在此之前不重新发起请求（避免 429 循环）。</summary>
        public DateTime FailureBackoffUntilUtc = DateTime.MinValue;

        public Task? InFlight;

        public readonly List<Action<RemoteSnapshot<TValue>>> Subscribers = [];
    }

    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly TimeSpan                           ttl;
    private readonly TimeSpan                           failureTtl;

    public RemoteQueryCache
    (
        TimeSpan ttl,
        TimeSpan failureTtl
    )
    {
        this.ttl        = ttl;
        this.failureTtl = failureTtl;
    }

    /// <summary>
    /// 发起（或被合并进）一次请求，并立即返回当前快照。
    /// 已有新鲜数据时不重复请求；数据就绪后通过 <see cref="Observe"/> 回调通知。
    /// </summary>
    public RemoteSnapshot<TValue> GetOrRequest
    (
        string                                key,
        Func<CancellationToken, Task<TValue>> fetch
    )
    {
        _ = EnsureFetch(key, fetch);

        return entries.TryGetValue(key, out var entry) ?
                   entry.Snapshot :
                   default;
    }

    /// <summary>
    /// 订阅某个 key 的数据变化。注册时会立即回调一次当前快照（若已有数据），
    /// 返回的对象 Dispose 后即取消订阅。
    /// </summary>
    public IDisposable Observe
    (
        string                                        key,
        Func<CancellationToken, Task<TValue>>         fetch,
        Action<RemoteSnapshot<TValue>>                onUpdate
    )
    {
        var entry = entries.GetOrAdd(key, static _ => new());

        lock (entry.Subscribers)
            entry.Subscribers.Add(onUpdate);

        if (entry.Snapshot.Status != RemoteSnapshotStatus.Empty)
            SafeInvoke(onUpdate, entry.Snapshot);

        _ = EnsureFetch(key, fetch);

        return new Subscription(() =>
        {
            lock (entry.Subscribers)
                entry.Subscribers.Remove(onUpdate);
        });
    }

    public bool TryGet
    (
        string                         key,
        out RemoteSnapshot<TValue>     snapshot
    )
    {
        if (entries.TryGetValue(key, out var entry))
        {
            snapshot = entry.Snapshot;
            return true;
        }

        snapshot = default;
        return false;
    }

    private Task EnsureFetch
    (
        string                                key,
        Func<CancellationToken, Task<TValue>> fetch
    )
    {
        var entry = entries.GetOrAdd(key, static _ => new());

        lock (entry)
        {
            if (entry.InFlight is { IsCompleted: false })
                return entry.InFlight;

            if (DateTime.UtcNow < entry.ExpiresAtUtc)
                return Task.CompletedTask;

            // 失败退避：请求失败（如 429）后不要立刻重试，否则会形成 429 循环
            if (DateTime.UtcNow < entry.FailureBackoffUntilUtc)
                return Task.CompletedTask;

            entry.Snapshot = entry.Snapshot with
            {
                Status = entry.Snapshot.HasValue ?
                             RemoteSnapshotStatus.Refreshing :
                             RemoteSnapshotStatus.Loading
            };

            Notify(entry);
            entry.InFlight = RunAsync(entry, fetch);
            return entry.InFlight;
        }
    }

    private async Task RunAsync
    (
        Entry                                 entry,
        Func<CancellationToken, Task<TValue>> fetch
    )
    {
        try
        {
            var value = await fetch(CancellationToken.None).ConfigureAwait(false);

            lock (entry)
            {
                entry.Snapshot               = new(RemoteSnapshotStatus.Ready, value, DateTime.UtcNow, null);
                entry.ExpiresAtUtc           = DateTime.UtcNow + ttl;
                entry.FailureCount           = 0;
                entry.FailureBackoffUntilUtc = DateTime.MinValue;
            }
        }
        catch (Exception ex)
        {
            DLog.Error($"[AutoBuyer] Universalis 请求失败: {typeof(TValue).Name}", ex);

            // 指数退避：20s → 40s → 80s（上限 180s），避免失败后立刻重试造成 429 循环
            lock (entry)
            {
                entry.FailureCount++;

                var backoffSeconds = Math.Min(20 * Math.Pow(2, entry.FailureCount - 1), 180);

                entry.FailureBackoffUntilUtc = DateTime.UtcNow + TimeSpan.FromSeconds(backoffSeconds);
            }

            lock (entry)
            {
                entry.Snapshot     = entry.Snapshot with { Status = RemoteSnapshotStatus.Failed, Error = ex };
                entry.ExpiresAtUtc = DateTime.UtcNow + failureTtl;
            }
        }

        Notify(entry);
    }

    private static void Notify
    (
        Entry entry
    )
    {
        Action<RemoteSnapshot<TValue>>[] subscribers;

        lock (entry.Subscribers)
            subscribers = [.. entry.Subscribers];

        if (subscribers.Length == 0)
            return;

        // 回调方会访问游戏状态与 UI，统一切到 Framework 线程执行
        var framework = DService.Instance().Framework;

        if (framework.IsInFrameworkUpdateThread)
        {
            foreach (var subscriber in subscribers)
                SafeInvoke(subscriber, entry.Snapshot);
        }
        else
        {
            _ = framework.RunOnFrameworkThread(() =>
            {
                foreach (var subscriber in subscribers)
                    SafeInvoke(subscriber, entry.Snapshot);
            });
        }
    }

    private static void SafeInvoke
    (
        Action<RemoteSnapshot<TValue>> callback,
        RemoteSnapshot<TValue>         snapshot
    )
    {
        try
        {
            callback(snapshot);
        }
        catch (Exception ex)
        {
            DLog.Error("[AutoBuyer] Universalis 数据回调发生错误", ex);
        }
    }

    private sealed class Subscription
    (
        Action onDispose
    ) : IDisposable
    {
        private Action? onDispose = onDispose;

        public void Dispose() =>
            Interlocked.Exchange(ref onDispose, null)?.Invoke();
    }
}
