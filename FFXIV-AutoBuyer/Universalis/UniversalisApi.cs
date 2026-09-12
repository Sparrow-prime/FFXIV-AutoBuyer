using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FFXIVAutoBuyer.Universalis;

/// <summary>
/// Universalis REST API 访问层（<c>https://universalis.app</c>，API v2）。
/// </summary>
internal static class UniversalisApi
{
    public const string BaseUrl = "https://universalis.app";

    /// <summary>国服大区（Region）名，取自 <c>GET /api/v2/data-centers</c> 的实际返回值。</summary>
    public const string ChinaRegionName = "中国";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling           = JsonNumberHandling.AllowReadingFromString
    };

    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri(BaseUrl),
        Timeout     = TimeSpan.FromSeconds(30)
    };

    static UniversalisApi()
    {
        // 明确标识客户端（含仓库地址）：便于服务端识别，降低被判定为滥用/脚本的风险
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("FFXIV-AutoBuyer/1.0 (+https://github.com/Sparrow-prime/FFXIV-AutoBuyer)");
    }

    public static async Task<T> GetAsync<T>
    (
        string            path,
        CancellationToken cancellationToken = default
    ) where T : class
    {
        using var response = await Http.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                                        .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);

        return result ?? throw new InvalidOperationException($"Universalis 响应反序列化失败: {path}");
    }

    /// <summary>拼接查询串（仅包含非空参数）。</summary>
    public static string BuildQuery
    (
        bool? hq            = null,
        int?  listings      = null,
        int?  entries       = null,
        long? statsWithin   = null,
        long? entriesWithin = null
    )
    {
        var parts = new List<string>(5);

        if (hq.HasValue)
            parts.Add($"hq={hq.Value.ToString().ToLowerInvariant()}");

        if (listings is > 0)
            parts.Add($"listings={listings.Value}");

        if (entries is > 0)
            parts.Add($"entries={entries.Value}");

        if (statsWithin is > 0)
            parts.Add($"statsWithin={statsWithin.Value}");

        if (entriesWithin is > 0)
            parts.Add($"entriesWithin={entriesWithin.Value}");

        return parts.Count == 0 ?
                   string.Empty :
                   $"?{string.Join('&', parts)}";
    }
}
