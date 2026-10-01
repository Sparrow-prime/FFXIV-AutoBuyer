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

    /// <summary>
    /// Universalis 文档（https://docs.universalis.app/）的明文规范与这里的对应关系：
    /// <list type="number">
    /// <item>「API 限速 25 req/s（突发 50）」→ <see cref="RateLimiter"/>：令牌桶 20 req/s、突发 40，留出余量；</item>
    /// <item>「每 IP 同时连接数上限 8」→ <see cref="ConcurrencyGate"/>：并发槽位 8，响应体读完前一直持有。</item>
    /// </list>
    /// 本客户端是 HTTP/1.1（<see cref="HttpClient"/> 未指定版本时的默认值），一个在途请求即占用一条连接，
    /// 因此「在途请求数」等于「连接数」，上面第二道闸门直接对应规范里的连接上限。
    /// </summary>
    private const int    MAX_CONCURRENT_REQUESTS = 8;
    private const double REQUESTS_PER_SECOND     = 20;
    private const double BURST_CAPACITY          = 40;

    private static readonly SemaphoreSlim     ConcurrencyGate = new(MAX_CONCURRENT_REQUESTS, MAX_CONCURRENT_REQUESTS);
    private static readonly RequestRateLimiter RateLimiter    = new(REQUESTS_PER_SECOND, BURST_CAPACITY);

    public static async Task<T> GetAsync<T>
    (
        string            path,
        CancellationToken cancellationToken = default
    ) where T : class
    {
        // 两道闸门，顺序固定：先取速率令牌（限「每秒发多少」），再占并发槽位（限「同时有多少在途」）。
        await RateLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        await ConcurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var response = await Http.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                                            .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);

            return result ?? throw new InvalidOperationException($"Universalis 响应反序列化失败: {path}");
        }
        finally
        {
            // 响应体读完（或异常）后才归还槽位：这期间那条 HTTP/1.1 连接一直被这个请求占用
            ConcurrencyGate.Release();
        }
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

    /// <summary>
    /// 令牌桶限速器（对应文档「25 req/s、突发 50」，此处取 20 req/s、突发 40）。
    /// 令牌不足时**异步等待**，不阻塞调用线程；本类所有请求都已在异步链上（<c>ConfigureAwait(false)</c>），
    /// 因此不会占住游戏主线程。
    /// </summary>
    private sealed class RequestRateLimiter
    {
        private readonly object gate = new();
        private readonly double refillPerSecond;
        private readonly double capacity;

        private double tokens;
        private long   lastRefillTick;

        public RequestRateLimiter
        (
            double refillPerSecond,
            double capacity
        )
        {
            this.refillPerSecond = refillPerSecond;
            this.capacity        = capacity;

            tokens         = capacity;
            lastRefillTick = Environment.TickCount64;
        }

        public async Task WaitAsync
        (
            CancellationToken cancellationToken
        )
        {
            while (true)
            {
                TimeSpan wait;

                lock (gate)
                {
                    Refill();

                    if (tokens >= 1)
                    {
                        tokens -= 1;
                        return;
                    }

                    wait = TimeSpan.FromMilliseconds(Math.Max(1, (1 - tokens) / refillPerSecond * 1000));
                }

                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>按经过的时间补充令牌（调用方须持有 <see cref="gate"/>）。</summary>
        private void Refill()
        {
            var now     = Environment.TickCount64;
            var elapsed = (now - lastRefillTick) / 1000.0;

            if (elapsed <= 0)
                return;

            tokens         = Math.Min(capacity, tokens + elapsed * refillPerSecond);
            lastRefillTick = now;
        }
    }
}
