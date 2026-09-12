using DailyRoutines.Common.RemoteInteraction.Models;

namespace FFXIVAutoBuyer.Universalis;

/// <summary>
/// 历史成交数据源（Universalis <c>/api/v2/history/{worldDcRegion}/{itemIds}</c>）。
/// </summary>
public static class RemoteUniversalisHistory
{
    private static readonly RemoteQueryCache<UniversalisMarketHistoryResponse> Cache =
        new(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(30));

    public static RemoteSnapshot<UniversalisMarketHistoryResponse> GetOrRequest
    (
        uint[]                                  itemIDs,
        string                                  worldName,
        UniversalisMarketHistoryRequestParams?  requestParams = null
    ) =>
        Cache.GetOrRequest(BuildKey(itemIDs, worldName, requestParams), ct => FetchAsync(itemIDs, worldName, requestParams, ct));

    public static IDisposable Observe
    (
        uint[]                                                   itemIDs,
        string                                                   worldName,
        Action<RemoteSnapshot<UniversalisMarketHistoryResponse>>  onUpdate,
        UniversalisMarketHistoryRequestParams?                    requestParams = null
    ) =>
        Cache.Observe
        (
            BuildKey(itemIDs, worldName, requestParams),
            ct => FetchAsync(itemIDs, worldName, requestParams, ct),
            onUpdate
        );

    public static bool TryGet
    (
        uint[]                                  itemIDs,
        string                                  worldName,
        out RemoteSnapshot<UniversalisMarketHistoryResponse> snapshot,
        UniversalisMarketHistoryRequestParams?  requestParams = null
    ) =>
        Cache.TryGet(BuildKey(itemIDs, worldName, requestParams), out snapshot);

    private static string BuildKey
    (
        uint[]                                  itemIDs,
        string                                  worldName,
        UniversalisMarketHistoryRequestParams?  requestParams
    ) =>
        $"history|{worldName}|{string.Join(',', itemIDs)}|{requestParams?.EntriesToReturn}";

    private static async Task<UniversalisMarketHistoryResponse> FetchAsync
    (
        uint[]                                  itemIDs,
        string                                  worldName,
        UniversalisMarketHistoryRequestParams?  requestParams,
        CancellationToken                       cancellationToken
    )
    {
        var query = UniversalisApi.BuildQuery
        (
            requestParams?.HQ,
            null,
            requestParams?.EntriesToReturn,
            requestParams?.StatsWithin,
            requestParams?.EntriesWithin
        );

        var path = $"/api/v2/history/{Uri.EscapeDataString(worldName)}/{string.Join(',', itemIDs)}{query}";

        // 单物品请求返回裸的 HistoryView，这里统一归一化为多物品形态
        if (itemIDs.Length == 1)
        {
            var single = await UniversalisApi.GetAsync<UniversalisHistoryItemData>(path, cancellationToken).ConfigureAwait(false);

            return new()
            {
                Items      = new() { [itemIDs[0]] = single },
                WorldIDRaw = single.WorldID,
                WorldName  = single.WorldName,
                DcName     = single.DcName,
                RegionName = single.RegionName
            };
        }

        return await UniversalisApi.GetAsync<UniversalisMarketHistoryResponse>(path, cancellationToken).ConfigureAwait(false);
    }
}
