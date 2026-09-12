using DailyRoutines.Common.RemoteInteraction.Models;

namespace FFXIVAutoBuyer.Universalis;

/// <summary>
/// 当前挂牌数据源（Universalis <c>/api/v2/{worldDcRegion}/{itemIds}</c>）。
/// </summary>
public static class RemoteUniversalisMarket
{
    private static readonly RemoteQueryCache<UniversalisMarketDataResponse> Cache =
        // 有效期 5 分钟（原 60 秒）：该数据仅作为「查看其他世界」时的回退列表，
        // 且 Universalis API 更新滞后，缩短重拉间隔并无收益。
        new(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30));

    public static RemoteSnapshot<UniversalisMarketDataResponse> GetOrRequest
    (
        uint[]                                 itemIDs,
        string                                 worldName,
        UniversalisMarketDataRequestParams?    requestParams = null
    ) =>
        Cache.GetOrRequest(BuildKey(itemIDs, worldName, requestParams), ct => FetchAsync(itemIDs, worldName, requestParams, ct));

    public static IDisposable Observe
    (
        uint[]                                                    itemIDs,
        string                                                    worldName,
        Action<RemoteSnapshot<UniversalisMarketDataResponse>>      onUpdate,
        UniversalisMarketDataRequestParams?                       requestParams = null
    ) =>
        Cache.Observe
        (
            BuildKey(itemIDs, worldName, requestParams),
            ct => FetchAsync(itemIDs, worldName, requestParams, ct),
            onUpdate
        );

    public static bool TryGet
    (
        uint[]                                 itemIDs,
        string                                 worldName,
        out RemoteSnapshot<UniversalisMarketDataResponse> snapshot,
        UniversalisMarketDataRequestParams?    requestParams = null
    ) =>
        Cache.TryGet(BuildKey(itemIDs, worldName, requestParams), out snapshot);

    private static string BuildKey
    (
        uint[]                              itemIDs,
        string                              worldName,
        UniversalisMarketDataRequestParams? requestParams
    ) =>
        $"market|{worldName}|{string.Join(',', itemIDs)}|{requestParams?.HQ}";

    private static async Task<UniversalisMarketDataResponse> FetchAsync
    (
        uint[]                              itemIDs,
        string                              worldName,
        UniversalisMarketDataRequestParams? requestParams,
        CancellationToken                   cancellationToken
    )
    {
        var query = UniversalisApi.BuildQuery
        (
            requestParams?.HQ,
            requestParams?.ListingsToReturn,
            requestParams?.EntriesToReturn,
            requestParams?.StatsWithin,
            requestParams?.EntriesWithin
        );

        var path = $"/api/v2/{Uri.EscapeDataString(worldName)}/{string.Join(',', itemIDs)}{query}";

        // 单物品请求返回裸的 CurrentlyShownView，这里统一归一化为多物品形态
        if (itemIDs.Length == 1)
        {
            var single = await UniversalisApi.GetAsync<UniversalisMarketItemData>(path, cancellationToken).ConfigureAwait(false);

            return new()
            {
                Items      = new() { [itemIDs[0]] = single },
                WorldIDRaw = single.WorldID,
                WorldName  = single.WorldName,
                DcName     = single.DcName,
                RegionName = single.RegionName
            };
        }

        return await UniversalisApi.GetAsync<UniversalisMarketDataResponse>(path, cancellationToken).ConfigureAwait(false);
    }
}
