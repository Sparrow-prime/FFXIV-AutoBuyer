using DailyRoutines.Common.RemoteInteraction.Models;

namespace FFXIVAutoBuyer.Universalis;

/// <summary>
/// 聚合数据源（Universalis <c>/api/v2/aggregated/{worldDcRegion}/{itemIds}</c>）。
/// <paramref name="scope"/> 可为世界名、数据中心名或大区名。
/// </summary>
public static class RemoteUniversalisAggregatedMarket
{
    private static readonly RemoteQueryCache<UniversalisAggregatedMarketDataResponse> Cache =
        // 有效期 15 分钟：Universalis 的 API 更新明显滞后于其网页（新上传数据网页立即可见、
        // API 很久才更新），因此频繁重拉没有意义，反而增加被判滥用的风险。
        new(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(30));

    public static RemoteSnapshot<UniversalisAggregatedMarketDataResponse> GetOrRequest
    (
        uint[]   itemIDs,
        string   scope
    ) =>
        Cache.GetOrRequest(BuildKey(itemIDs, scope), ct => FetchAsync(itemIDs, scope, ct));

    public static IDisposable Observe
    (
        uint[]                                                          itemIDs,
        string                                                          scope,
        Action<RemoteSnapshot<UniversalisAggregatedMarketDataResponse>>  onUpdate
    ) =>
        Cache.Observe
        (
            BuildKey(itemIDs, scope),
            ct => FetchAsync(itemIDs, scope, ct),
            onUpdate
        );

    public static bool TryGet
    (
        uint[]                                                    itemIDs,
        string                                                    scope,
        out RemoteSnapshot<UniversalisAggregatedMarketDataResponse> snapshot
    ) =>
        Cache.TryGet(BuildKey(itemIDs, scope), out snapshot);

    private static string BuildKey
    (
        uint[] itemIDs,
        string scope
    ) =>
        $"aggregated|{scope}|{string.Join(',', itemIDs)}";

    private static Task<UniversalisAggregatedMarketDataResponse> FetchAsync
    (
        uint[]            itemIDs,
        string            scope,
        CancellationToken cancellationToken
    )
    {
        var path = $"/api/v2/aggregated/{Uri.EscapeDataString(scope)}/{string.Join(',', itemIDs)}";
        return UniversalisApi.GetAsync<UniversalisAggregatedMarketDataResponse>(path, cancellationToken);
    }
}
