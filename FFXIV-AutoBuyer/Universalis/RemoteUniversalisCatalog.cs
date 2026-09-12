using OmenTools.Dalamud;

namespace FFXIVAutoBuyer.Universalis;

/// <summary>
/// Universalis 世界与数据中心目录（进程内缓存，一次拉取长期复用）。
/// </summary>
public static class RemoteUniversalisCatalog
{
    private static readonly object Gate = new();

    private static List<UniversalisDataCenter>? dataCenters;
    private static List<UniversalisWorld>?      worlds;

    private static Task? dataCentersTask;
    private static Task? worldsTask;

    /// <summary>上次拉取失败的时刻：失败后进入冷却，避免每 500ms 重试造成 429 循环。</summary>
    private static long lastFailureTick;

    private const long CATALOG_RETRY_COOLDOWN_MS = 30_000;

    private static bool InFailureCooldown =>
        Environment.TickCount64 - lastFailureTick < CATALOG_RETRY_COOLDOWN_MS;

    /// <summary>请求（或复用）数据中心列表。</summary>
    public static Task GetDataCentersOrRequest()
    {
        lock (Gate)
        {
            if (dataCenters is { Count: > 0 })
                return Task.CompletedTask;

            if (InFailureCooldown)
                return Task.CompletedTask;

            return dataCentersTask is { IsCompleted: false } ?
                       dataCentersTask :
                       dataCentersTask = FetchDataCentersAsync();
        }
    }

    /// <summary>请求（或复用）世界列表。</summary>
    public static Task GetWorldsOrRequest()
    {
        lock (Gate)
        {
            if (worlds is { Count: > 0 })
                return Task.CompletedTask;

            if (InFailureCooldown)
                return Task.CompletedTask;

            return worldsTask is { IsCompleted: false } ?
                       worldsTask :
                       worldsTask = FetchWorldsAsync();
        }
    }

    public static bool TryGetDataCenters
    (
        out List<UniversalisDataCenter> result
    )
    {
        lock (Gate)
        {
            result = dataCenters ?? [];
            return result.Count > 0;
        }
    }

    public static bool TryGetWorlds
    (
        out List<UniversalisWorld> result
    )
    {
        lock (Gate)
        {
            result = worlds ?? [];
            return result.Count > 0;
        }
    }

    private static async Task FetchDataCentersAsync()
    {
        try
        {
            var result = await UniversalisApi.GetAsync<List<UniversalisDataCenter>>("/api/v2/data-centers").ConfigureAwait(false);

            lock (Gate)
                dataCenters = result;
        }
        catch (Exception ex)
        {
            DLog.Error("[AutoBuyer] 获取 Universalis 数据中心列表失败", ex);

            lock (Gate)
            {
                lastFailureTick = Environment.TickCount64;
                dataCentersTask = null; // 冷却结束后允许重试
            }
        }
    }

    private static async Task FetchWorldsAsync()
    {
        try
        {
            var result = await UniversalisApi.GetAsync<List<UniversalisWorld>>("/api/v2/worlds").ConfigureAwait(false);

            lock (Gate)
                worlds = result;
        }
        catch (Exception ex)
        {
            DLog.Error("[AutoBuyer] 获取 Universalis 世界列表失败", ex);

            lock (Gate)
            {
                lastFailureTick = Environment.TickCount64;
                worldsTask      = null;
            }
        }
    }
}
