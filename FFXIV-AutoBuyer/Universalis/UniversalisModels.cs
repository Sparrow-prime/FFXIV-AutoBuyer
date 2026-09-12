using System.Text.Json.Serialization;

namespace FFXIVAutoBuyer.Universalis;

#region 目录（世界 / 数据中心）

/// <summary>
/// Universalis <c>GET /api/v2/worlds</c> 的元素。
/// </summary>
public sealed class UniversalisWorld
{
    [JsonPropertyName("id")]
    public uint ID { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Universalis <c>GET /api/v2/data-centers</c> 的元素。
/// </summary>
public sealed class UniversalisDataCenter
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>大区（Region）名，国服为「中国」。</summary>
    [JsonPropertyName("region")]
    public string Region { get; init; } = string.Empty;

    [JsonPropertyName("worlds")]
    public uint[] Worlds { get; init; } = [];
}

#endregion

#region 当前挂牌

/// <summary>
/// 单个物品在单个世界/大区/大区的当前挂牌数据。
/// 对应 Universalis V2 <c>CurrentlyShownView</c>（单物品请求时为裸对象）。
/// </summary>
public sealed class UniversalisMarketItemData
{
    [JsonPropertyName("itemID")]
    public uint ItemID { get; init; }

    [JsonPropertyName("worldID")]
    public uint? WorldID { get; init; }

    [JsonPropertyName("worldName")]
    public string? WorldName { get; init; }

    [JsonPropertyName("dcName")]
    public string? DcName { get; init; }

    [JsonPropertyName("regionName")]
    public string? RegionName { get; init; }

    /// <summary>该数据最后一次上传时间（毫秒时间戳）。</summary>
    [JsonPropertyName("lastUploadTime")]
    public long LastUploadTime { get; init; }

    [JsonPropertyName("listings")]
    public List<UniversalisMarketListing>? Listings { get; init; }

    [JsonPropertyName("recentHistory")]
    public List<UniversalisHistorySale>? RecentHistory { get; init; }

    /// <summary>数据最后上传时间（UTC）。</summary>
    public DateTime GetLastUploadTime() =>
        DateTimeOffset.FromUnixTimeMilliseconds(LastUploadTime).UtcDateTime;
}

/// <summary>
/// Universalis 挂牌条目。
/// </summary>
public sealed class UniversalisMarketListing
{
    [JsonPropertyName("listingID")]
    public string ListingID { get; init; } = string.Empty;

    /// <summary>单价。</summary>
    [JsonPropertyName("pricePerUnit")]
    public ulong PricePerUnit { get; init; }

    [JsonPropertyName("quantity")]
    public uint Quantity { get; init; }

    /// <summary>是否为高品质（HQ）。</summary>
    [JsonPropertyName("hq")]
    public bool HQ { get; init; }

    /// <summary>是否陈列在模特上。</summary>
    [JsonPropertyName("onMannequin")]
    public bool OnMannequin { get; init; }

    [JsonPropertyName("isCrafted")]
    public bool IsCrafted { get; init; }

    /// <summary>含税总价。</summary>
    [JsonPropertyName("total")]
    public ulong Total { get; init; }

    /// <summary>交易税。</summary>
    [JsonPropertyName("tax")]
    public ulong Tax { get; init; }

    [JsonPropertyName("retainerName")]
    public string? RetainerName { get; init; }

    [JsonPropertyName("creatorName")]
    public string? CreatorName { get; init; }

    [JsonPropertyName("materia")]
    public List<UniversalisMateria>? Materia { get; init; }

    /// <summary>上架时间（秒时间戳）。</summary>
    [JsonPropertyName("lastReviewTime")]
    public long LastReviewTime { get; init; }

    public DateTime GetLastReviewTime() =>
        DateTimeOffset.FromUnixTimeSeconds(LastReviewTime).UtcDateTime;
}

public sealed class UniversalisMateria
{
    [JsonPropertyName("slotID")]
    public int SlotID { get; init; }

    [JsonPropertyName("materiaID")]
    public int MateriaID { get; init; }
}

/// <summary>
/// 多物品当前挂牌响应（Universalis V2 <c>CurrentlyShownMultiViewV2</c>）。
/// 本客户端对单物品请求也会归一化为此形态，便于调用方统一处理。
/// </summary>
public sealed class UniversalisMarketDataResponse
{
    [JsonPropertyName("items")]
    public Dictionary<uint, UniversalisMarketItemData> Items { get; init; } = [];

    [JsonPropertyName("worldID")]
    public uint? WorldIDRaw { get; init; }

    [JsonPropertyName("worldName")]
    public string? WorldName { get; init; }

    [JsonPropertyName("dcName")]
    public string? DcName { get; init; }

    [JsonPropertyName("regionName")]
    public string? RegionName { get; init; }

    /// <summary>本次响应归属的世界 ID。</summary>
    public uint WorldID =>
        WorldIDRaw ?? Items.Values.Select(x => x.WorldID).FirstOrDefault(x => x is > 0) ?? 0;

    /// <summary>数据最后上传时间（UTC）。</summary>
    public DateTime GetLastUploadTime() =>
        Items.Values.Select(x => x.GetLastUploadTime()).DefaultIfEmpty(DateTime.MinValue).Max();
}

#endregion

#region 历史成交

/// <summary>
/// 历史成交条目（Universalis <c>MinimizedSaleView</c>）。
/// </summary>
public sealed class UniversalisHistorySale
{
    [JsonPropertyName("hq")]
    public bool HQ { get; init; }

    [JsonPropertyName("pricePerUnit")]
    public ulong PricePerUnit { get; init; }

    [JsonPropertyName("quantity")]
    public uint Quantity { get; init; }

    [JsonPropertyName("onMannequin")]
    public bool OnMannequin { get; init; }

    [JsonPropertyName("buyerName")]
    public string? BuyerName { get; init; }

    /// <summary>成交时间（秒时间戳）。</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    public DateTime GetSaleTime() =>
        DateTimeOffset.FromUnixTimeSeconds(Timestamp).UtcDateTime;
}

/// <summary>
/// 单个物品的历史成交数据（Universalis V2 <c>HistoryView</c>）。
/// </summary>
public sealed class UniversalisHistoryItemData
{
    [JsonPropertyName("itemID")]
    public uint ItemID { get; init; }

    [JsonPropertyName("worldID")]
    public uint? WorldID { get; init; }

    [JsonPropertyName("worldName")]
    public string? WorldName { get; init; }

    [JsonPropertyName("dcName")]
    public string? DcName { get; init; }

    [JsonPropertyName("regionName")]
    public string? RegionName { get; init; }

    [JsonPropertyName("lastUploadTime")]
    public long LastUploadTime { get; init; }

    [JsonPropertyName("entries")]
    public List<UniversalisHistorySale>? Entries { get; init; }
}

/// <summary>
/// 多物品历史成交响应（本客户端对单物品请求同样归一化为该形态）。
/// </summary>
public sealed class UniversalisMarketHistoryResponse
{
    [JsonPropertyName("items")]
    public Dictionary<uint, UniversalisHistoryItemData> Items { get; init; } = [];

    [JsonPropertyName("worldID")]
    public uint? WorldIDRaw { get; init; }

    [JsonPropertyName("worldName")]
    public string? WorldName { get; init; }

    [JsonPropertyName("dcName")]
    public string? DcName { get; init; }

    [JsonPropertyName("regionName")]
    public string? RegionName { get; init; }

    public uint WorldID =>
        WorldIDRaw ?? Items.Values.Select(x => x.WorldID).FirstOrDefault(x => x is > 0) ?? 0;
}

#endregion

#region 聚合数据

public sealed class UniversalisAggregatedMarketDataResponse
{
    [JsonPropertyName("results")]
    public List<UniversalisAggregatedMarketResult> Results { get; init; } = [];

    [JsonPropertyName("failedItems")]
    public List<uint> FailedItems { get; init; } = [];
}

public sealed class UniversalisAggregatedMarketResult
{
    [JsonPropertyName("itemId")]
    public uint ItemID { get; init; }

    [JsonPropertyName("nq")]
    public UniversalisAggregatedMarketScope NQ { get; init; } = new();

    [JsonPropertyName("hq")]
    public UniversalisAggregatedMarketScope HQ { get; init; } = new();
}

public sealed class UniversalisAggregatedMarketScope
{
    [JsonPropertyName("minListing")]
    public UniversalisMinListing MinListing { get; init; } = new();

    [JsonPropertyName("medianListing")]
    public UniversalisMedianListing? MedianListing { get; init; }

    [JsonPropertyName("recentPurchase")]
    public UniversalisRecentPurchase RecentPurchase { get; init; } = new();

    [JsonPropertyName("averageSalePrice")]
    public UniversalisAverageSalePrice AverageSalePrice { get; init; } = new();

    [JsonPropertyName("dailySaleVelocity")]
    public UniversalisDailySaleVelocity DailySaleVelocity { get; init; } = new();
}

/// <summary>最低挂牌价（分世界 / 大区 / 大区范围三个口径）。</summary>
public sealed class UniversalisMinListing
{
    [JsonPropertyName("world")]
    public UniversalisMinListingEntry World { get; init; } = new();

    [JsonPropertyName("dc")]
    public UniversalisMinListingEntry Dc { get; init; } = new();

    [JsonPropertyName("region")]
    public UniversalisMinListingEntry Region { get; init; } = new();
}

public sealed class UniversalisMinListingEntry
{
    [JsonPropertyName("price")]
    public double? Price { get; init; }

    [JsonPropertyName("worldID")]
    public uint? WorldID { get; init; }
}

public sealed class UniversalisMedianListing
{
    [JsonPropertyName("world")]
    public UniversalisPriceEntry World { get; init; } = new();

    [JsonPropertyName("dc")]
    public UniversalisPriceEntry Dc { get; init; } = new();

    [JsonPropertyName("region")]
    public UniversalisPriceEntry Region { get; init; } = new();
}

public sealed class UniversalisPriceEntry
{
    [JsonPropertyName("price")]
    public double? Price { get; init; }
}

public sealed class UniversalisRecentPurchase
{
    [JsonPropertyName("world")]
    public UniversalisRecentPurchaseEntry World { get; init; } = new();

    [JsonPropertyName("dc")]
    public UniversalisRecentPurchaseEntry Dc { get; init; } = new();

    [JsonPropertyName("region")]
    public UniversalisRecentPurchaseEntry Region { get; init; } = new();
}

public sealed class UniversalisRecentPurchaseEntry
{
    [JsonPropertyName("price")]
    public double? Price { get; init; }

    /// <summary>成交时间（毫秒时间戳）。</summary>
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }

    [JsonPropertyName("worldID")]
    public uint? WorldID { get; init; }
}

public sealed class UniversalisAverageSalePrice
{
    [JsonPropertyName("world")]
    public UniversalisPriceEntry World { get; init; } = new();

    [JsonPropertyName("dc")]
    public UniversalisPriceEntry Dc { get; init; } = new();

    [JsonPropertyName("region")]
    public UniversalisPriceEntry Region { get; init; } = new();
}

public sealed class UniversalisDailySaleVelocity
{
    [JsonPropertyName("world")]
    public UniversalisVelocityEntry World { get; init; } = new();

    [JsonPropertyName("dc")]
    public UniversalisVelocityEntry Dc { get; init; } = new();

    [JsonPropertyName("region")]
    public UniversalisVelocityEntry Region { get; init; } = new();
}

public sealed class UniversalisVelocityEntry
{
    [JsonPropertyName("quantity")]
    public float? Quantity { get; init; }
}

#endregion

#region 请求参数

public sealed class UniversalisMarketDataRequestParams
{
    /// <summary>仅返回 HQ（true）或 NQ（false）挂牌。</summary>
    public bool? HQ { get; init; }

    public int? ListingsToReturn { get; init; }

    public int? EntriesToReturn { get; init; }

    /// <summary>统计口径时间窗口（毫秒）。</summary>
    public long? StatsWithin { get; init; }

    /// <summary>历史条目时间窗口（毫秒）。</summary>
    public long? EntriesWithin { get; init; }
}

public sealed class UniversalisMarketHistoryRequestParams
{
    public bool? HQ { get; init; }

    public int? EntriesToReturn { get; init; }

    public long? StatsWithin { get; init; }

    public long? EntriesWithin { get; init; }
}

#endregion
