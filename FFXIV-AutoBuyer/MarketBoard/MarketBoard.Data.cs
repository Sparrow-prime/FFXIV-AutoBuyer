using FFXIVAutoBuyer.Universalis;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using OmenTools.Info.Game.ItemSource;
using OmenTools.Info.Game.ItemSource.Enums;
using OmenTools.Dalamud;
using OmenTools.Interop.Game.Lumina;
using OmenTools.OmenService;
using OmenTools.Threading;

namespace FFXIVAutoBuyer.MarketBoard;

public unsafe partial class MarketBoardModule
{
    private readonly record struct SearchCategoryGroup
    (
        ItemSearchCategory Category,
        List<Item>         Items
    );

    private sealed class CachedValue<T>
    {
        public int Version = -1;
        public T?  Value;
    }

    private sealed class HistoryDataSet
    {
        public List<HistoryEntry> Entries = [];
        public int                TotalCount;
        public uint               TotalQty;
        public ulong              AvgPrice;
        public int                HQCount;
        public int                HQPercent;
        public bool               IsCanBeHQ;
        public bool               IsAnyHQ;
        public ulong              AvgNQPrice;
        public ulong              AvgHQPrice;
    }

    private sealed class ListingsDataSet
    {
        public List<UniversalisMarketListing> Listings = [];
        public UniversalisMarketItemData?     Source;
        public bool                           IsAnyHQ;
        public bool                           IsAnyOnMannequin;
        public int                            TotalCount;
        public uint                           TotalQty;
    }

    private sealed class LocalListingsDataSet
    {
        public List<MarketBoardListing> Listings = [];
        public bool                     IsAnyHQ;
        public bool                     IsAnyOnMannequin;
        public bool                     IsAnyMateria;
        public int                      TotalCount;
        public uint                     TotalQty;
    }

    private sealed class WorldPriceRanks
    (
        List<RankedWorldPriceRow> valid,
        List<RankedWorldPriceRow> cheapest,
        List<RankedWorldPriceRow> expensive,
        WorldPriceRow             current
    )
    {
        public List<RankedWorldPriceRow> Valid     = valid;
        public List<RankedWorldPriceRow> Cheapest  = cheapest;
        public List<RankedWorldPriceRow> Expensive = expensive;
        public WorldPriceRow             Current   = current;
    }

    private sealed class MarketDataProvider
    (
        MarketBoardModule owner
    )
    {
        public uint   SelectedWorldID     { get; private set; }

        /// <summary>大区（Region）固定为「中国」：国服插件不再提供大区切换。</summary>
        public string EffectiveRegionName { get; private set; } = UniversalisApi.ChinaRegionName;
        public bool   HQOnly;

        public uint SelectedItemID { get; private set; }

        public long LastSelectTime { get; private set; }

        public bool IsViewingCurrentWorld =>
            SelectedWorldID == GameState.CurrentWorld;

        /// <summary>
        /// 我方是否仍在等待「当前所选物品」的本地搜索完成。
        /// 为真时游戏侧 <c>SearchItemId</c> 可能仍是上一个物品，界面不应跟随游戏侧物品。
        /// </summary>
        public bool IsLocalListingsStale => localListingsStale;

        public WorldPriceRow MinPriceData { get; private set; }

        public WorldPriceRow MaxPriceData { get; private set; }

        public IReadOnlyDictionary<string, List<WorldPriceRow>> DCWorldPrices => cachedDCWorldPrices;

        /// <summary>
        /// 隐式刷新进行中（UI 据此显示「刷新中…」而不隐藏列表）。
        /// 到达兜底截止时间后自动视为结束，避免状态卡死。
        /// </summary>
        public bool IsImplicitRefreshPending =>
            pendingImplicitRefresh && Environment.TickCount64 < implicitRefreshDeadline;

        private int onlineDataVersion;
        private int onlineHistoryVersion;
        private int itemEpoch;

        private readonly Dictionary<(uint ItemID, uint WorldID, bool HQOnly), UniversalisMarketDataResponse> onlineDataCache   = [];
        private readonly Dictionary<(uint ItemID, uint WorldID, bool HQOnly), IDisposable>                   subscriptionCache = [];

        private readonly Dictionary<(uint ItemID, uint WorldID), UniversalisAggregatedMarketDataResponse> onlineAggregatedCache       = [];
        private readonly Dictionary<(uint ItemID, uint WorldID), IDisposable>                             aggregatedSubscriptionCache = [];

        private readonly Dictionary<(uint ItemID, uint WorldID), UniversalisMarketHistoryResponse> onlineHistoryCache       = [];
        private readonly Dictionary<(uint ItemID, uint WorldID), IDisposable>                      historySubscriptionCache = [];

        private readonly Dictionary<(uint ItemID, string Scope), IDisposable> tooltipAggregatedSubscriptionCache = [];

        private readonly Dictionary<string, List<WorldPriceRow>> cachedDCWorldPrices = [];
        private          WorldPriceRanks?                        worldPriceRanks;

        /// <summary>当前价格表对应的物品 ID（用于切换物品时立即作废旧数据）。</summary>
        private uint priceTableItemID;

        /// <summary>
        /// 跨服后是否已确认「游戏侧数据属于新世界」。
        /// 仅在观察到「列表被清空 → 重新到齐」的完整循环后才置为 true，
        /// 避免把上一服务器的残留挂牌当作新服务器数据（并污染服务器最低价缓存）。
        /// </summary>
        private bool worldDataRefreshed = true;

        /// <summary>跨服后是否已观察到「列表未就绪」的状态（确认清空已生效）。</summary>
        private bool worldDataNotReadySeen;
        private          string?                                 priceTableRegion;
        private          bool                                    priceTableHQOnly;
        private          bool                                    priceTableOnlyCurrentDC;
        private          bool                                    worldPriceTableDirty = true;

        private readonly Dictionary<(uint ItemID, uint WorldID, bool HQOnly), CachedValue<HistoryDataSet>>  historyDataCache  = [];
        private readonly Dictionary<(uint ItemID, uint WorldID, bool HQOnly), CachedValue<ListingsDataSet>> listingsDataCache = [];

        private (int ItemEpoch, uint ItemID, bool HQOnly, uint ListingCount, int ContentHash) localListingsFingerprint;
        private LocalListingsDataSet?                                                         localListingsData;

        private bool                                                    localListingsStale;
        private long                                                    localSearchRetryDeadline;
        private int                                                     localSearchRetryAttempts;
        private long                                                    localSearchNextRetryTick;
        private (uint SearchItemId, uint EntryCount, uint ListingCount) localListingsBaseline;

        /// <summary>
        /// 本地列表补拉参数：重获取上限 1 次（加上初次请求最多 2 次）、
        /// 首次等待 3 秒、后续间隔 5 秒、总窗口 12 秒。
        /// 参数刻意保守：游戏市场列表本身刷新较慢，频繁补拉会表现成「持续刷新」。
        /// </summary>
        // 补拉（单次获取 + 失败重试）策略。
        // 注意：跨服后玩家需要重新走到市场布告板，IsAbleToSearchMarket() 才会为真，
        // 因此总窗口只在「可以搜索」的时间段内消耗（不可搜索 / 服务器拒绝时自动续期），
        // 否则窗口会在玩家还没走到布告板前就过期，导致一直取不到数据。
        /// <summary>允许的自动补拉次数（每次仅请求 1 个物品）。</summary>
        private const int LOCAL_SEARCH_RETRY_MAX_ATTEMPTS = 4;

        /// <summary>首次补拉延迟：避开跨服落地瞬间（此时游戏侧尚不能搜索）。</summary>
        private const long LOCAL_SEARCH_RETRY_FIRST_DELAY_MS = 5_000;

        /// <summary>两次补拉之间的间隔。</summary>
        private const long LOCAL_SEARCH_RETRY_INTERVAL_MS = 10_000;

        /// <summary>补拉总窗口（仅在可搜索时计时）。</summary>
        private const long LOCAL_SEARCH_RETRY_TOTAL_MS = 60_000;

        /// <summary>隐式刷新进行中：请求新数据但继续显示当前列表。</summary>
        private bool pendingImplicitRefresh;

        /// <summary>隐式刷新的兜底截止时间，避免「刷新中…」无限持续。</summary>
        private long implicitRefreshDeadline;

        /// <summary>最近一次本地市场搜索请求时刻（用于限流，避免连续刷新游戏列表）。</summary>
        private static long lastLocalSearchTick;

        /// <summary>两次本地搜索请求的最小间隔（毫秒，全局不限物品）。</summary>
        private const long LOCAL_SEARCH_MIN_INTERVAL_MS = 800;

        /// <summary>上一次请求在途时的最长等待时间（毫秒），超时后允许重新下发。</summary>
        private const long LOCAL_SEARCH_PENDING_WAIT_MS = 3_000;

        /// <summary>
        /// 服务器拒绝市场数据请求后的冷却截止时刻（毫秒）。
        /// 游戏对高频请求会回以「请稍后再次确认」错误码，继续下发只会形成
        /// 「请求 → 被拒 → 再请求」的刷新循环，因此这里强制静默一段时间。
        /// </summary>
        private static long marketRejectionCooldownUntil;

        /// <summary>服务器拒绝后的静默时长（毫秒）。</summary>
        private const long MARKET_REJECTION_COOLDOWN_MS = 20_000;

        /// <summary>Universalis 各缓存的最大条目数，超过则整体清空一次。</summary>
        private const int CACHE_ENTRY_LIMIT = 2_000;

        /// <summary>
        /// 「服务器（游戏内）」最低价短时缓存。
        /// 跨服/刷新间隙游戏数据会短暂不可用，此时优先使用最近一次读到的服务器价格，
        /// 避免回落到可能不准确的 Universalis 数据。
        /// </summary>
        private readonly Dictionary<(uint ItemID, uint WorldID, bool HQOnly), (ulong MinPrice, long Tick)> gameMinPriceCache = [];

        /// <summary>服务器最低价缓存有效期（毫秒）：半小时。</summary>
        private const long GAME_MIN_PRICE_TTL_MS = 30 * 60 * 1000;

        /// <summary>聚合数据的请求记录（分批遍历时跳过已请求过的世界，避免配额被前几个世界反复占用）。</summary>
        private readonly Dictionary<(uint ItemID, uint WorldID), long> aggregatedRequestTicks = [];

        /// <summary>
        /// 聚合数据的重拉间隔（与 RemoteUniversalisAggregatedMarket 的 TTL 保持一致）。
        /// 15 分钟：Universalis API 更新滞后于网页，频繁重拉无收益。
        /// </summary>
        private const long AGGREGATED_DATA_TTL_MS = 15 * 60 * 1000;

        /// <summary>
        /// 单次调用最多处理的「新请求」世界数：单一大区的世界数硬上限为 8，
        /// 因此一次调用即可覆盖整个大区；其余大区由每秒一次的 tick 继续补齐，避免请求风暴。
        /// </summary>
        private const int MAX_AGGREGATED_WORLDS_PER_CALL = 8;

        /// <summary>
        /// 请求刷新道具提示（限流）。
        /// 选择一次物品会收到 28 个世界的回调，逐个触发会让单帧耗时超过 100ms（日志中的 HITCH）。
        /// </summary>
        private static void RequestTooltipDetailUpdate()
        {
            if (Throttler.Shared.Throttle("BetterMarketBoard-TooltipDetailUpdate", 500))
                TooltipManager.Instance().TriggerItemDetailUpdate();
        }

        /// <summary>最近窗口内的自动请求记录（熔断用）：(物品, 时刻)。</summary>
        private static readonly Queue<(uint ItemID, long Tick)> autoRequestTicks = [];

        private static long autoRequestSuspendedUntil;
        private static long lastDiagLogTick;
        private static int  diagSuppressedCount;

        /// <summary>
        /// 熔断参数：**同一物品** 60 秒内自动请求超过 10 次即暂停 120 秒。
        /// 浏览不同物品不计入熔断（只针对「同一物品被反复请求」的异常循环）；
        /// 另有全局上限 30 次 / 60 秒作为兜底。
        /// </summary>
        private const int  AUTO_REQUEST_WINDOW_MS      = 60_000;
        private const int  AUTO_REQUEST_MAX_PER_ITEM   = 10;
        private const int  AUTO_REQUEST_GLOBAL_SAFETY  = 30;
        private const long AUTO_REQUEST_SUSPEND_MS     = 120_000;

        /// <summary>
        /// 是否输出诊断日志（由模块配置项 <c>EnableDiagnostics</c> 控制，默认关闭）。
        /// 需要排查问题时在插件设置里打开，游戏内用 <c>/xllog</c> 过滤 <c>[AutoBuyer]</c> 查看。
        /// </summary>
        public static bool DiagnosticsEnabled;

        /// <summary>诊断日志（默认关闭，开启时限流输出）。</summary>
        public static void DiagLog
        (
            string message
        )
        {
            if (!DiagnosticsEnabled)
                return;

            var now = Environment.TickCount64;

            if (now - lastDiagLogTick < 200)
            {
                diagSuppressedCount++;
                return;
            }

            lastDiagLogTick = now;

            var suffix = diagSuppressedCount > 0 ?
                             $"（另有 {diagSuppressedCount} 条日志被限流）" :
                             string.Empty;

            diagSuppressedCount = 0;

            DLog.Warning($"[AutoBuyer][诊断] {message}{suffix}");
        }

        /// <summary>自动请求熔断检查；返回 true 表示允许继续。</summary>
        private static bool PassAutoRequestBreaker
        (
            uint itemID,
            long now
        )
        {
            if (now < autoRequestSuspendedUntil)
                return false;

            while (autoRequestTicks.Count > 0 && now - autoRequestTicks.Peek().Tick > AUTO_REQUEST_WINDOW_MS)
                autoRequestTicks.Dequeue();

            var sameItemCount = autoRequestTicks.Count(x => x.ItemID == itemID);

            if (sameItemCount >= AUTO_REQUEST_MAX_PER_ITEM ||
                autoRequestTicks.Count >= AUTO_REQUEST_GLOBAL_SAFETY)
            {
                autoRequestSuspendedUntil = now + AUTO_REQUEST_SUSPEND_MS;
                autoRequestTicks.Clear();

                DiagLog($"同一物品自动请求过于频繁（item={itemID}，{sameItemCount} 次 / {AUTO_REQUEST_WINDOW_MS / 1000} 秒），已暂停自动请求 {AUTO_REQUEST_SUSPEND_MS / 1000} 秒；可点刷新按钮手动恢复");

                return false;
            }

            autoRequestTicks.Enqueue((itemID, now));
            return true;
        }

        private readonly Dictionary<uint, ItemSourceInfo?> itemSourceCache  = [];
        private readonly Dictionary<uint, uint?>           npcGilPriceCache = [];

        private string?                    searchInputCache;
        private List<Item>?                searchResultRefCache;
        private List<SearchCategoryGroup>? searchGroupsCache;
        #region 选择与请求

        public void SelectItem
        (
            uint    itemID,
            string? regionName   = null,
            bool?   hqOnly       = null,
            bool    forceRefresh = false,
            string  reason       = ""
        )
        {
            var info = InfoProxy;
            if (info == null) return;

            DiagLog($"SelectItem item={itemID} reason={reason} hq={hqOnly?.ToString() ?? "-"} force={forceRefresh}");

            var targetHQOnly = hqOnly ?? HQOnly;

            if (targetHQOnly && (!LuminaGetter.TryGetRow<Item>(itemID, out var itemData) || !itemData.CanBeHq))
                targetHQOnly = false;

            var isNewItem     = SelectedItemID != itemID || HQOnly != targetHQOnly;
            var isItemChanged = SelectedItemID != itemID;

            if (isItemChanged)
                itemEpoch++;

            SelectedItemID = itemID;
            HQOnly         = targetHQOnly;
            LastSelectTime = Environment.TickCount64;

            if (isNewItem)
            {
                ClearAllData();
            }

            // 注意：这里**不要**写 info->SearchItemId。
            // 直接改写游戏侧搜索目标会让游戏自己侦测到变化并主动发起搜索，
            // 从而绕过本插件所有的节流与熔断（表现为商品列表持续刷新、游戏提示「获取过于频繁」）。
            // 搜索目标只在真正发起请求时（RequestLocalSearchData）写入。

            RequestAllWorldsData(itemID, regionName, targetHQOnly);

            if (IsViewingCurrentWorld && (forceRefresh || !GameState.Instance().IsMarketListingsStuck))
            {
                if (IsAbleToSearchMarket())
                {
                    // 请求可能因「上一次尚未完成/限流」被跳过，此时交给补拉机制稍后重试
                    if (!RequestLocalSearchData(itemID, forceRefresh, $"SelectItem:{reason}"))
                        MarkLocalListingsStale(info, $"SelectItem:{reason}");
                }
                else
                    info->ClearListData();
            }
        }

        public void SelectWorld
        (
            uint worldID
        )
        {
            if (SelectedItemID == 0) return;

            if (SelectedWorldID != worldID)
            {
                DisposeSelectedWorldData(SelectedItemID, SelectedWorldID);
                ClearDerivedCaches();
                localListingsData        = null;
                localListingsFingerprint = default;
                onlineDataVersion++;
                onlineHistoryVersion++;
            }

            SelectedWorldID = worldID;
            SelectItem(SelectedItemID, null, HQOnly, reason: "切换世界");
        }
        public void ToggleHQ()
        {
            if (SelectedItemID == 0) return;

            SelectItem(SelectedItemID, null, !HQOnly, reason: "切换HQ");
        }

        /// <summary>玩家主动刷新：清空缓存并强制重新获取（忽略繁忙冷却）。</summary>
        public void Reload()
        {
            if (SelectedItemID == 0) return;

            ClearAllData();
            SelectItem(SelectedItemID, null, HQOnly, forceRefresh: true, reason: "手动刷新");
            MarkLocalListingsStale(InfoProxy, "手动刷新");
        }

        public void AnchorWorld() =>
            SelectedWorldID = GameState.CurrentWorld;

        public void EnsureAnchored()
        {
            if (SelectedWorldID == 0)
                AnchorWorld();

            AnchorRegion();
        }

        /// <summary>
        /// 游戏侧市场数据在当前世界是否可用。
        /// 跨服后必须先观察到「清空 → 重新到齐」的完整循环：游戏侧在跨服瞬间仍持有
        /// 上一服务器的挂牌，若直接采用会把旧服务器价格显示（并缓存）成本服数据。
        /// </summary>
        private bool IsGameMarketDataUsable
        (
            uint itemID
        )
        {
            if (InfoProxy == null || !IsAbleToSearchLocalMarket() || InfoProxy->SearchItemId != itemID)
                return false;

            var isReady = InfoProxy->IsFullyReceived();

            if (!worldDataRefreshed)
            {
                if (!isReady)
                {
                    // 清空已生效：等待重新到齐
                    worldDataNotReadySeen = true;
                    return false;
                }

                if (!worldDataNotReadySeen)
                {
                    DiagLog($"忽略疑似旧世界残留的游戏数据 item={itemID}");
                    return false;
                }

                worldDataRefreshed = true;
                DiagLog($"新世界的游戏侧数据已就绪 item={itemID}");
            }

            return isReady;
        }

        /// <summary>
        /// 检测到跨服 / 世界切换时**立刻**作废旧世界的全部显示数据（不发起任何请求）。
        /// 与 <see cref="ResyncAfterWorldChange"/> 的区别：本方法在发现世界变化的当帧即执行，
        /// 不去抖等待；世界重同步仍按原节奏处理（用于锚定世界目录与补拉标记）。
        /// </summary>
        public void InvalidateWorldData
        (
            string reason
        )
        {
            worldDataRefreshed    = false;
            worldDataNotReadySeen = false;

            SelectedWorldID = GameState.CurrentWorld;

            var info = InfoProxy;

            if (info != null)
                info->ClearListData();

            cachedDCWorldPrices.Clear();
            worldPriceRanks          = null;
            MinPriceData             = default;
            MaxPriceData             = default;
            worldPriceTableDirty     = true;
            localListingsData        = null;
            localListingsFingerprint = default;
            localListingsBaseline    = default;
            localSearchRetryAttempts = 0;

            // 交给补拉机制在新世界重新搜索（窗口未打开时不会发起任何请求）
            localListingsStale       = true;
            localSearchRetryDeadline = Environment.TickCount64 + LOCAL_SEARCH_RETRY_TOTAL_MS;
            localSearchNextRetryTick = Environment.TickCount64 + LOCAL_SEARCH_RETRY_FIRST_DELAY_MS;

            DiagLog($"跨服/世界切换：已作废旧世界数据（{reason}）");
        }

        public void ResyncAfterWorldChange()
        {
            DiagLog($"世界重同步 world={GameState.CurrentWorld} item={SelectedItemID}");

            SelectedWorldID     = GameState.CurrentWorld;
            EffectiveRegionName = UniversalisApi.ChinaRegionName;

            // 世界数据已在「发现世界变化」的当帧由 InvalidateWorldData 作废并标记补拉；
            // 此处仅在尚未作废（兜底路径）时清理，避免把刚取到的新世界数据再清一次、白跑一次请求。
            if (worldDataRefreshed)
            {
                var info = InfoProxy;
                if (info != null)
                    info->ClearListData();

                ClearAllData();

                // 跨服后不再立即发起游戏搜索（减少与游戏服务器通信），
                // 仅标记待补拉：若布告板窗口开着，稍后由补拉机制补 1 次；窗口关着则完全不请求。
                MarkLocalListingsStale(info, "世界重同步");
            }
        }

        /// <summary>让卡片区的世界价格表在下次绘制时重建（世界目录变化后调用）。</summary>
        public void MarkPriceTableDirty()
        {
            cachedDCWorldPrices.Clear();
            worldPriceRanks      = null;
            worldPriceTableDirty = true;
        }

        /// <summary>请求补一次本地列表（仅一次；成功后由 <c>IsFullyReceived</c> 判定停止）。</summary>
        public void RequestRefreshOnce
        (
            string reason
        ) =>
            MarkLocalListingsStale(InfoProxy, reason);

        /// <summary>
        /// 仅切换界面显示用的物品，不向游戏发起搜索请求
        /// （用于插件启动时采用游戏当前物品，避免启动即产生一次游戏服务器通信）。
        /// </summary>
        public void AdoptItemWithoutSearch
        (
            uint   itemID,
            string reason = ""
        )
        {
            if (itemID == 0) return;

            DiagLog($"采用物品（不搜索）item={itemID} reason={reason}");

            if (SelectedItemID != itemID)
            {
                itemEpoch++;
                ClearAllData();
            }

            SelectedItemID = itemID;
            LastSelectTime = Environment.TickCount64;

            EnsureAnchored();
            RequestAllWorldsData(itemID, null, HQOnly);
        }

        /// <summary>标记本地列表需要重新获取（等待补拉机制按「单次获取 + 失败重试」策略处理）。</summary>
        private void MarkLocalListingsStale
        (
            InfoProxyItemSearch* info,
            string               reason = ""
        )
        {
            DiagLog($"标记本地列表待补拉 reason={reason}");

            localListingsStale       = true;
            localSearchRetryAttempts = 0;
            localSearchNextRetryTick = Environment.TickCount64 + LOCAL_SEARCH_RETRY_FIRST_DELAY_MS;
            localSearchRetryDeadline = Environment.TickCount64 + LOCAL_SEARCH_RETRY_TOTAL_MS;
            localListingsBaseline    = info == null ?
                                           default :
                                           (info->SearchItemId, info->EntryCount, info->ListingCount);
        }

        /// <summary>
        /// 服务器拒绝了市场数据请求（游戏提示「请稍后再次确认」）。
        /// 进入静默冷却并结束当前补拉，避免继续请求形成刷新循环。
        /// </summary>
        public void NotifyMarketRequestRejected()
        {
            DiagLog($"服务器拒绝市场数据请求，进入 {MARKET_REJECTION_COOLDOWN_MS / 1000} 秒静默冷却");

            marketRejectionCooldownUntil = Environment.TickCount64 + MARKET_REJECTION_COOLDOWN_MS;
            localListingsStale           = false;
            localSearchRetryAttempts     = 0;
            localSearchNextRetryTick     = 0;
        }

        /// <summary>
        /// 本地列表补拉（跨服/启动后）。
        /// 策略：**单次获取 → 失败才重获取 → 成功即不再获取**。
        /// 成功以「游戏已完整返回当前物品的在售列表」为准（<c>IsFullyReceived</c>），
        /// 一旦成功立即结束补拉，之后不再重复请求，避免游戏列表被反复刷新。
        /// </summary>
        public void RetryLocalSearchIfStale()
        {
            if (!localListingsStale) return;

            var info = InfoProxy;

            // 成功：数据已完整到达 → 结束补拉
            if (info != null && info->SearchItemId == SelectedItemID && info->IsFullyReceived(SelectedItemID))
            {
                localListingsStale       = false;
                localSearchRetryAttempts = 0;
                return;
            }

            if (SelectedItemID == 0 || !IsViewingCurrentWorld)
            {
                localListingsStale = false;
                return;
            }

            var now = Environment.TickCount64;

            // 尚不能搜索（跨服途中 / 布告板未打开）：窗口自动续期，等待玩家真正能搜索的时刻
            if (!IsAbleToSearchMarket())
            {
                localSearchRetryDeadline = now + LOCAL_SEARCH_RETRY_TOTAL_MS;
                return;
            }

            // 服务器正在拒绝请求：不消耗窗口与尝试次数，等冷却结束再补
            if (GameState.Instance().IsMarketListingsStuck)
            {
                localSearchRetryDeadline = now + LOCAL_SEARCH_RETRY_TOTAL_MS;
                return;
            }

            if (now > localSearchRetryDeadline)
            {
                DiagLog($"补拉窗口结束（未取到数据）item={SelectedItemID} 尝试={localSearchRetryAttempts}");
                localListingsStale = false;
                return;
            }

            if (now < localSearchNextRetryTick) return;

            // 重获取次数用尽 → 放弃（等待玩家手动切物品/刷新）
            if (localSearchRetryAttempts >= LOCAL_SEARCH_RETRY_MAX_ATTEMPTS)
            {
                localListingsStale = false;
                return;
            }

            localSearchRetryAttempts++;
            localSearchNextRetryTick = now + LOCAL_SEARCH_RETRY_INTERVAL_MS;

            DiagLog($"补拉重试 第 {localSearchRetryAttempts} 次 item={SelectedItemID}");

            RequestLocalSearchData(SelectedItemID, reason: "补拉重试");
        }

        public void AnchorRegion() =>
            EffectiveRegionName = UniversalisApi.ChinaRegionName;

        /// <summary>
        /// 隐式刷新：请求新数据但不隐藏当前列表；<c>GetLocalListingsDataSet</c> 等到指纹变化后
        /// 才原子替换数据，期间 UI 继续渲染旧列表。
        /// </summary>
        public void BeginImplicitRefresh
        (
            uint itemID
        )
        {
            if (itemID == 0) return;

            var info = InfoProxy;

            localListingsBaseline    = info == null ?
                                           default :
                                           (info->SearchItemId, info->EntryCount, info->ListingCount);
            pendingImplicitRefresh   = true;
            implicitRefreshDeadline  = Environment.TickCount64 + 3_000;

            // 被节流跳过时交给补拉机制，保证购买后列表终会刷新
            if (!RequestLocalSearchData(itemID, reason: "购买后隐式刷新", countForBreaker: false))
                MarkLocalListingsStale(InfoProxy, "购买后隐式刷新");
        }

        public void EnsurePriceData
        (
            uint itemID,
            bool hqOnly
        )
        {
            if (itemID == 0 || owner.allWorlds.Count == 0)
                return;

            EnsureAnchored();
            RequestAllWorldsData(itemID, null, hqOnly);
        }

        /// <summary>
        /// 请求游戏重新拉取本地市场布告板列表。
        /// 同一物品在 <see cref="LOCAL_SEARCH_MIN_INTERVAL_MS"/> 内重复调用会被忽略，
        /// 避免购买等流程高频触发导致游戏列表持续刷新。
        /// </summary>
        public static bool RequestLocalSearchData
        (
            uint   itemID,
            bool   force           = false,
            string reason          = "",
            bool   countForBreaker = true
        )
        {
            if (itemID == 0 || InfoProxy == null)
                return false;

            var now = Environment.TickCount64;

            // force 用于玩家主动刷新：忽略静默冷却、限流与熔断，允许立刻重试一次
            if (!force)
            {
                // 服务器刚拒绝过请求：静默期内一律不下发
                if (now < marketRejectionCooldownUntil)
                {
                    DiagLog($"请求被跳过（服务器拒绝冷却中）item={itemID} reason={reason}");
                    return false;
                }

                // 游戏市场列表处于「请稍后再次确认」状态时不下发新请求
                if (GameState.Instance().IsMarketListingsStuck)
                {
                    DiagLog($"请求被跳过（游戏列表繁忙）item={itemID} reason={reason}");
                    return false;
                }

                // 熔断：同一物品被反复自动请求说明存在异常循环
                // （购买后的隐式刷新属于正常流程，不计入熔断）
                if (countForBreaker && !PassAutoRequestBreaker(itemID, now))
                {
                    DiagLog($"请求被跳过（熔断中）item={itemID} reason={reason}");
                    return false;
                }

                // 全局（不区分物品）最小间隔：连续切换物品时避免高频下发搜索请求
                if (now - lastLocalSearchTick < LOCAL_SEARCH_MIN_INTERVAL_MS)
                {
                    DiagLog($"请求被跳过（全局节流）item={itemID} reason={reason}");
                    return false;
                }

                // 上一次请求尚未完成且未超时 → 等待其结束，避免游戏市场列表被反复重置
                // （表现为列表持续刷新，甚至提示「请重新选择物品」）
                var pendingItemID = InfoProxy->SearchItemId;

                if (pendingItemID != 0                                      &&
                    now - lastLocalSearchTick < LOCAL_SEARCH_PENDING_WAIT_MS &&
                    !InfoProxy->IsFullyReceived(pendingItemID))
                {
                    DiagLog($"请求被跳过（上一次在途）pending={pendingItemID} item={itemID} reason={reason}");
                    return false;
                }
            }
            else
                marketRejectionCooldownUntil = 0;

            lastLocalSearchTick = now;

            DiagLog($"【发起市场搜索请求】item={itemID} reason={reason} force={force}");

            InfoProxy->EndRequest();
            InfoProxy->SearchItemId = itemID;
            return InfoProxy->RequestData();
        }

        public static bool SendBuyRequest
        (
            MarketBoardListing item
        )
        {
            if (IsOwnRetainer(item.RetainerId))
            {
                // 无法购买自己的雇员所出售的道具。
                RaptureLogModule.Instance()->ShowLogMessage(468);
                return false;
            }

            InfoProxy->SetLastPurchasedItem(&item);
            return InfoProxy->SendPurchaseRequestPacket();
        }

        public void ClearAllData()
        {
            DisposeAllSubscriptions();

            subscriptionCache.Clear();
            historySubscriptionCache.Clear();
            aggregatedSubscriptionCache.Clear();
            tooltipAggregatedSubscriptionCache.Clear();

            // 注意：保留 onlineDataCache / onlineAggregatedCache / onlineHistoryCache。
            // 它们以 (物品, 世界) 为键、跨物品复用；若在此清空，每次切换物品都会重新向
            // Universalis 拉取 28 个世界的数据，既慢又会触发 429 Too Many Requests。
            // 仅在体量异常时整体清一次，避免长期运行内存无界增长。
            if (onlineDataCache.Count       > CACHE_ENTRY_LIMIT) onlineDataCache.Clear();
            if (onlineAggregatedCache.Count > CACHE_ENTRY_LIMIT) onlineAggregatedCache.Clear();
            if (onlineHistoryCache.Count    > CACHE_ENTRY_LIMIT) onlineHistoryCache.Clear();

            ClearDerivedCaches();
            cachedDCWorldPrices.Clear();
            worldPriceRanks          = null;
            worldPriceTableDirty     = true;
            localListingsData        = null;
            localListingsFingerprint = default;
            localListingsStale       = false;
            localSearchRetryDeadline = 0;
            localListingsBaseline    = default;
            itemEpoch++;
            onlineDataVersion++;
            onlineHistoryVersion++;
        }

        private void ClearDerivedCaches()
        {
            historyDataCache.Clear();
            listingsDataCache.Clear();
        }

        private void DisposeSelectedWorldData
        (
            uint itemID,
            uint worldID
        )
        {
            if (worldID == 0)
                return;

            foreach (var key in subscriptionCache.Keys
                                                 .Where(key => key.ItemID == itemID && key.WorldID == worldID)
                                                 .ToList())
            {
                subscriptionCache[key].Dispose();
                subscriptionCache.Remove(key);
                onlineDataCache.Remove(key);
            }

            var historyKey = (ItemID: itemID, WorldID: worldID);
            if (historySubscriptionCache.Remove(historyKey, out var historySubscription))
                historySubscription.Dispose();

            onlineHistoryCache.Remove(historyKey);
        }

        private void DisposeAllSubscriptions()
        {
            foreach (var subscription in subscriptionCache.Values)
            {
                if (subscription != null)
                    subscription.Dispose();
            }

            foreach (var subscription in historySubscriptionCache.Values)
            {
                if (subscription != null)
                    subscription.Dispose();
            }

            foreach (var subscription in aggregatedSubscriptionCache.Values)
            {
                if (subscription != null)
                    subscription.Dispose();
            }

            foreach (var subscription in tooltipAggregatedSubscriptionCache.Values)
            {
                if (subscription != null)
                    subscription.Dispose();
            }
        }

        private bool RequestAllWorldsData
        (
            uint    itemID,
            string? regionName = null,
            bool    hqOnly     = false
        )
        {
            if (owner.allWorlds.Count == 0) return false;

            Dictionary<string, Dictionary<uint, string>>? targetRegion = null;
            var targetRegionName = !string.IsNullOrEmpty(regionName) ?
                                       regionName :
                                       EffectiveRegionName;

            if (!string.IsNullOrEmpty(targetRegionName))
                owner.allWorlds.TryGetValue(targetRegionName, out targetRegion);

            targetRegion ??= owner.allWorlds.Values.FirstOrDefault(r => r.Values.Any(dc => dc.ContainsKey(GameState.CurrentWorld)));

            if (targetRegion == null)
                return false;

            // 勾选「仅显示当前大区数据」时只请求当前数据中心（约 7 个世界而非 28 个）：
            // 既符合显示语义，也显著减少与 Universalis 的通信量。
            var dcsToProcess = targetRegion;

            if (owner.config.OnlyCurrentDC)
            {
                var currentDCName = GetCurrentDCName();

                if (!string.IsNullOrEmpty(currentDCName) && targetRegion.ContainsKey(currentDCName))
                    dcsToProcess = new() { [currentDCName] = targetRegion[currentDCName] };
            }

            var marketParam = new UniversalisMarketDataRequestParams
            {
                HQ = hqOnly
            };

            var epoch  = itemEpoch;
            var result = false;

            // 分批发起：一次调用最多处理 MAX_AGGREGATED_WORLDS_PER_CALL 个世界，
            // 其余交给每秒一次的 tick（EnsurePriceData）逐步补齐，
            // 避免启动/跨服瞬间 28 个请求并发触发 Universalis 429。
            var processedWorlds = 0;

            // 注意：必须「从上次中断的位置继续」，否则每轮都只处理排在最前的几个世界，
            // 排在末尾的世界（例如陆行鸟的 沃仙曦染 / 晨曦王座）将永远拿不到价格数据。
            foreach (var (worldID, worldName) in dcsToProcess.Values.SelectMany(static dc => dc))
            {
                if (string.IsNullOrEmpty(worldName))
                {
                    DiagLog($"跳过无名称的世界 world={worldID}（/worlds 与 /data-centers 数据不一致）");
                    continue;
                }

                var aggregatedCacheKey = (itemID, worldID);

                // 已请求过且仍在有效期内 → 不消耗本轮配额，让后面的世界有机会被处理
                if (aggregatedRequestTicks.TryGetValue(aggregatedCacheKey, out var lastRequestTick) &&
                    Environment.TickCount64 - lastRequestTick < AGGREGATED_DATA_TTL_MS)
                    continue;

                if (processedWorlds >= MAX_AGGREGATED_WORLDS_PER_CALL)
                    break; // 本轮配额用完，下一次 tick 继续

                processedWorlds++;
                aggregatedRequestTicks[aggregatedCacheKey] = Environment.TickCount64;

                _ = RemoteUniversalisAggregatedMarket.GetOrRequest([itemID], worldName);

                if (!aggregatedSubscriptionCache.ContainsKey(aggregatedCacheKey))
                {
                    aggregatedSubscriptionCache[aggregatedCacheKey] = RemoteUniversalisAggregatedMarket.Observe
                    (
                        [itemID],
                        worldName,
                        snapshot =>
                        {
                            if (epoch != itemEpoch)
                                return;

                            if (!snapshot.HasValue || snapshot.Value is not { } data)
                                return;

                            if (data.Results.All(x => x.ItemID != itemID))
                                return;

                            // 仅当该世界的最低报价真的变化时才重建卡表：
                            // 否则跨服/启动时 28 个世界的数据陆续到达会让卡片区反复重排。
                            var hadOldPrice = TryGetOnlineAggregatedMinPrice(itemID, worldID, out var oldMinPrice);

                            onlineAggregatedCache[aggregatedCacheKey] = data;

                            var hasNewPrice = TryGetOnlineAggregatedMinPrice(itemID, worldID, out var newMinPrice);

                            if (hadOldPrice != hasNewPrice ||
                                (hadOldPrice && oldMinPrice != newMinPrice))
                            {
                                DiagLog($"世界最低价变化 world={worldID} {oldMinPrice} -> {newMinPrice}");

                                worldPriceTableDirty = true;
                            }

                            RequestTooltipDetailUpdate();
                        }
                    );

                    result = true;
                }
            }

            var selectedWorldName = targetRegion.Values.SelectMany(static dc => dc)
                                                .FirstOrDefault(world => world.Key == SelectedWorldID)
                                                .Value;

            if (string.IsNullOrEmpty(selectedWorldName))
                return result;

            var marketCacheKey = (itemID, SelectedWorldID, hqOnly);

            // 本服在售列表可直接从游戏读取时，无需再向 Universalis 请求同一世界的挂牌数据
            var localDataAvailable = SelectedWorldID == GameState.CurrentWorld &&
                                     IsGameMarketDataUsable(itemID);

            if (!localDataAvailable)
            {
                _ = RemoteUniversalisMarket.GetOrRequest([itemID], selectedWorldName, marketParam);
            }

            if (!localDataAvailable && !subscriptionCache.ContainsKey(marketCacheKey))
            {
                subscriptionCache[marketCacheKey] = RemoteUniversalisMarket.Observe
                (
                    [itemID],
                    selectedWorldName,
                    snapshot =>
                    {
                        if (epoch != itemEpoch)
                            return;

                        if (!snapshot.HasValue || snapshot.Value is not { } data)
                            return;

                        if (SelectedWorldID != data.WorldID)
                            return;

                        onlineDataCache[marketCacheKey] = data;
                        onlineDataVersion++;
                        RequestTooltipDetailUpdate();
                    },
                    marketParam
                );

                result = true;
            }

            // 历史成交数据不再请求：成交均价已按需求移除，且唯一消费者 GetHistoryPercentilePrice 无调用者。
            // 保留该缓存与订阅管线会在每次选择物品时多产生 1 次 Universalis 请求，故一并停用。

            return result;
        }

        #endregion

        #region 派生数据

        private static T? GetOrBuild<T>
        (
            CachedValue<T?> slot,
            int             version,
            Func<T?>        build
        )
            where T : class
        {
            if (slot.Version != version)
            {
                slot.Value   = build();
                slot.Version = version;
            }

            return slot.Value;
        }

        private static HistoryEntry ToHistoryEntry
        (
            UniversalisHistorySale sale
        )
        {
            var saleTime = sale.GetSaleTime().ToLocalTime();
            return new(((DateTimeOffset)saleTime).ToUnixTimeSeconds(), saleTime, sale.PricePerUnit, sale.Quantity, sale.HQ);
        }

        public HistoryDataSet? GetHistoryDataSet
        (
            uint itemID,
            bool? hqOnly = null
        )
        {
            if (itemID == 0) return null;

            var targetHQOnly = hqOnly ?? HQOnly;
            var key  = (ItemID: itemID, WorldID: SelectedWorldID, HQOnly: targetHQOnly);
            var slot = historyDataCache.GetOrAdd(key, static _ => new());

            return GetOrBuild
            (
                slot,
                onlineHistoryVersion,
                () => BuildHistoryDataSet(itemID, key.WorldID, key.HQOnly)
            );
        }

        private HistoryDataSet? BuildHistoryDataSet
        (
            uint itemID,
            uint worldID,
            bool hqOnly
        )
        {
            if (!onlineHistoryCache.TryGetValue((itemID, worldID), out var response) ||
                !response.Items.TryGetValue(itemID, out var itemHistory)             ||
                itemHistory.Entries is not { Count: > 0 })
                return null;

            var isAnyHQ = itemHistory.Entries.Any(x => x.HQ);

            var entries = itemHistory.Entries
                                     .Where(x => x is { OnMannequin: false, PricePerUnit: > 0 } && (!hqOnly || x.HQ))
                                     .Select(ToHistoryEntry)
                                     .ToList();
            if (entries.Count == 0) return null;

            var isCanBeHQ   = LuminaGetter.TryGetRow<Item>(itemID, out var item) && item.CanBeHq;
            var totalCount  = entries.Count;
            var totalQty    = 0U;
            var totalAmount = 0.0;
            var hqCount     = 0;
            var nqAmount    = 0.0;
            var hqAmount    = 0.0;
            var nqQty       = 0U;
            var hqQty       = 0U;

            foreach (var entry in entries)
            {
                totalQty    += entry.Quantity;
                totalAmount += (double)entry.PricePerUnit * entry.Quantity;

                if (entry.IsHQ)
                {
                    hqCount++;
                    hqAmount += (double)entry.PricePerUnit * entry.Quantity;
                    hqQty    += entry.Quantity;
                }
                else
                {
                    nqAmount += (double)entry.PricePerUnit * entry.Quantity;
                    nqQty    += entry.Quantity;
                }
            }

            var avgPrice = totalQty > 0 ?
                               (ulong)Math.Round(totalAmount / totalQty) :
                               0;
            var hqPercent = (int)Math.Round((double)hqCount / totalCount * 100);

            ulong avgNQPrice = 0;
            ulong avgHQPrice = 0;

            if (isCanBeHQ)
            {
                avgNQPrice = nqQty > 0 ?
                                 (ulong)Math.Round(nqAmount / nqQty) :
                                 0;
                avgHQPrice = hqQty > 0 ?
                                 (ulong)Math.Round(hqAmount / hqQty) :
                                 0;
            }

            return new()
            {
                Entries    = entries,
                TotalCount = totalCount,
                TotalQty   = totalQty,
                AvgPrice   = avgPrice,
                HQCount    = hqCount,
                HQPercent  = hqPercent,
                IsCanBeHQ  = isCanBeHQ,
                IsAnyHQ    = isAnyHQ,
                AvgNQPrice = avgNQPrice,
                AvgHQPrice = avgHQPrice
            };
        }

        public ListingsDataSet? GetListingsDataSet
        (
            uint itemID
        )
        {
            if (itemID == 0) return null;

            var key  = (ItemID: itemID, WorldID: SelectedWorldID, HQOnly);
            var slot = listingsDataCache.GetOrAdd(key, static _ => new());

            return GetOrBuild
            (
                slot,
                onlineDataVersion,
                () => BuildListingsDataSet(itemID, key.WorldID, key.HQOnly)
            );
        }

        private ListingsDataSet? BuildListingsDataSet
        (
            uint itemID,
            uint worldID,
            bool hqOnly
        )
        {
            if (!onlineDataCache.TryGetValue((itemID, worldID, hqOnly), out var response) ||
                !response.Items.TryGetValue(itemID, out var uniItemData))
                return null;

            var listings = (uniItemData.Listings ?? [])
                           .Where(x => x.PricePerUnit > 0 && (x.HQ || !hqOnly))
                           .OrderBy(x => x.PricePerUnit)
                           .ToList();

            return new()
            {
                Listings         = listings,
                Source           = uniItemData,
                IsAnyHQ          = listings.Any(x => x.HQ),
                IsAnyOnMannequin = listings.Any(x => x.OnMannequin),
                TotalCount       = listings.Count,
                TotalQty         = listings.Aggregate(0U, (acc, l) => acc + l.Quantity)
            };
        }

        public LocalListingsDataSet GetLocalListingsDataSet
        (
            InfoProxyItemSearch* info
        )
        {
            if (localListingsStale)
            {
                var currentState = (info->SearchItemId, info->EntryCount, info->ListingCount);

                if (currentState == localListingsBaseline)
                {
                    DiagLog($"本地列表为空返回（等待新数据）item={info->SearchItemId} 基线={localListingsBaseline}");
                    return EmptyLocalListings();
                }

                localListingsStale       = false;
                localSearchRetryAttempts = 0;
            }

            if (!IsGameMarketDataUsable(info->SearchItemId))
            {
                // 跨服过渡期（含「新旧世界无法确认」）：一律不显示，避免显示上一服务器数据
                if (!worldDataRefreshed)
                {
                    DiagLog($"跨服过渡：本地列表暂不显示 item={info->SearchItemId}");
                    return EmptyLocalListings();
                }

                // 同世界内的隐式刷新：新数据成功获取前沿用上一次完整数据，不隐藏列表
                if (localListingsData != null)
                {
                    DiagLog($"本地列表暂未接收完整，沿用上一次数据 item={info->SearchItemId}");
                    return localListingsData;
                }

                DiagLog($"本地列表为空返回（游戏数据未接收完）item={info->SearchItemId} 条目={info->EntryCount}/{info->ListingCount}");
                return EmptyLocalListings();
            }

            var sourceListings = info->Listings.ToArray();
            var contentHash    = CalculateLocalListingsHash(sourceListings);
            var fingerprint    = (itemEpoch, info->SearchItemId, HQOnly, info->ListingCount, contentHash);
            if (localListingsData != null && localListingsFingerprint == fingerprint)
                return localListingsData;

            DiagLog($"本地列表重建 item={info->SearchItemId} HQ={HQOnly} 条目={info->ListingCount} 哈希={contentHash}");

            localListingsFingerprint = fingerprint;
            localListingsData        = BuildLocalListingsDataSet(info->SearchItemId, sourceListings);
            worldPriceTableDirty     = true;
            pendingImplicitRefresh   = false;
            return localListingsData;
        }

        private static readonly LocalListingsDataSet EmptyLocalListingsData = new();

        private static LocalListingsDataSet EmptyLocalListings() => EmptyLocalListingsData;

        private static int CalculateLocalListingsHash
        (
            IReadOnlyList<MarketBoardListing> listings
        )
        {
            var hash = new HashCode();

            // 归一化：游戏每次搜索返回的条目顺序可能不同（且会夹杂瞬时顺序变化），
            // 若直接按原顺序哈希，指纹会不断变化 → 本地列表反复重建（表现为商品列表持续刷新）。
            // 这里按 (单价, 数量, 挂牌 ID) 排序后再计算，使「同一批数据的不同顺序」得到同一指纹。
            foreach (var listing in listings.OrderBy(static x => x.UnitPrice)
                                            .ThenBy(static x => x.Quantity)
                                            .ThenBy(static x => x.ListingId))
            {
                hash.Add(listing.ListingId);
                hash.Add(listing.ItemId);
                hash.Add(listing.UnitPrice);
                hash.Add(listing.Quantity);
                hash.Add(listing.IsHqItem);
                hash.Add(listing.IsMannequin);
                hash.Add(listing.MateriaCount);
                hash.Add(listing.TotalTax);
            }

            return hash.ToHashCode();
        }

        private LocalListingsDataSet BuildLocalListingsDataSet
        (
            uint                              itemID,
            IReadOnlyList<MarketBoardListing> sourceListings
        )
        {
            var listingsArray = sourceListings
                                .Where(x => x.ItemId == itemID && x.UnitPrice != 0 && (x.IsHqItem || !HQOnly))
                                .OrderBy(x => x.UnitPrice)
                                .ToArray();

            var isAnyHQ          = listingsArray.Any(x => x.IsHqItem);
            var isAnyOnMannequin = listingsArray.Any(x => x.IsMannequin);
            var isAnyMateria = LuminaGetter.TryGetRow<Item>(itemID, out var itemData) &&
                               itemData.MateriaSlotCount > 0                          &&
                               listingsArray.Any(x => x.MateriaCount > 0);

            return new()
            {
                Listings         = [.. listingsArray],
                IsAnyHQ          = isAnyHQ,
                IsAnyOnMannequin = isAnyOnMannequin,
                IsAnyMateria     = isAnyMateria,
                TotalCount       = listingsArray.Length,
                TotalQty         = listingsArray.Aggregate(0U, (acc, l) => acc + l.Quantity)
            };
        }

        public List<SearchCategoryGroup> GetSearchGroups
        (
            string input
        )
        {
            var result = owner.searcher.SearchResult;
            if (searchGroupsCache != null  &&
                searchInputCache  == input &&
                ReferenceEquals(searchResultRefCache, result))
                return searchGroupsCache;

            searchInputCache     = input;
            searchResultRefCache = result;
            searchGroupsCache =
            [
                .. result.GroupBy(x => x.ItemSearchCategory.Value.RowId)
                         .Select(g => new SearchCategoryGroup(LuminaGetter.GetRowOrDefault<ItemSearchCategory>(g.Key), [.. g]))
            ];
            return searchGroupsCache;
        }

        #endregion

        #region 价格表

        public WorldPriceRanks? GetWorldPriceRanks
        (
            uint itemID
        )
        {
            var regionName = EffectiveRegionName;

            if (string.IsNullOrEmpty(regionName)                              ||
                !owner.allWorlds.TryGetValue(regionName, out var dcsInRegion) ||
                dcsInRegion.Count == 0)
            {
                worldPriceRanks = null;
                return null;
            }

            // 切换物品：立刻作废旧数据，避免卡片短暂显示上一物品的价格
            var itemChanged = priceTableItemID != itemID;

            if (itemChanged)
            {
                DiagLog($"切换物品 → 清空世界价格数据 item={itemID}");

                priceTableItemID     = itemID;
                cachedDCWorldPrices.Clear();
                worldPriceRanks      = null;
                MinPriceData         = default;
                MaxPriceData         = default;
                worldPriceTableDirty = true;
            }

            var stateChanged = itemChanged                                          ||
                               priceTableRegion        != regionName                ||
                               priceTableHQOnly        != HQOnly                    ||
                               priceTableOnlyCurrentDC != owner.config.OnlyCurrentDC ||
                               (InfoProxy != null && InfoProxy->EntryCount > 0 && cachedDCWorldPrices.Count == 0);
            // 注意：Throttler 默认仅 500ms，而跨服/启动时 28 个世界的最低价会陆续到达，
            // 若按默认节流会让卡片区以 ~2 次/秒的频率重排（表现为「连续刷新」），故放宽到 3 秒。
            var needRebuild = stateChanged ||
                              (worldPriceTableDirty && Throttler.Shared.Throttle("BetterMarketBoard-PriceTableUpdate", 3_000));

            if (needRebuild)
            {
                DiagLog($"价格表重建 region={regionName} HQ={HQOnly} 仅当前大区={owner.config.OnlyCurrentDC}");

                priceTableRegion        = regionName;
                priceTableHQOnly        = HQOnly;
                priceTableOnlyCurrentDC = owner.config.OnlyCurrentDC;
                cachedDCWorldPrices.Clear();

                foreach (var (dcName, worldsInDC) in dcsInRegion)
                {
                    var worldPricesList = new List<WorldPriceRow>();

                    foreach (var (worldID, worldName) in worldsInDC)
                    {
                        var minPrice = ulong.MaxValue;

                        // 1) 当前世界：服务器实时数据（游戏内市场列表）
                        if (worldID == GameState.CurrentWorld && IsGameMarketDataUsable(itemID))
                        {
                            var listings = InfoProxy->Listings.ToArray()
                                                              .Where
                                                              (x => x.ItemId    == itemID &&
                                                                    x.UnitPrice > 0       &&
                                                                    (x.IsHqItem || !HQOnly)
                                                              )
                                                              .ToList();

                            if (listings.Count > 0)
                            {
                                minPrice = listings.Min(x => x.UnitPrice);

                                // 记录「服务器最低价」缓存（半小时内有效）
                                gameMinPriceCache[(itemID, worldID, HQOnly)] = (minPrice, Environment.TickCount64);
                            }
                        }

                        // 2) 服务器最低价缓存：半小时内优先于 Universalis 显示
                        //    （跨服/刷新间隙游戏数据短暂不可用时，沿用最近一次读到的服务器价格）
                        if (minPrice == ulong.MaxValue                                              &&
                            gameMinPriceCache.TryGetValue((itemID, worldID, HQOnly), out var cachedGamePrice) &&
                            Environment.TickCount64 - cachedGamePrice.Tick < GAME_MIN_PRICE_TTL_MS)
                            minPrice = cachedGamePrice.MinPrice;

                        // 3) 最后才回落到 Universalis 聚合数据
                        if (minPrice == ulong.MaxValue)
                            _ = TryGetOnlineAggregatedMinPrice(itemID, worldID, out minPrice);

                        worldPricesList.Add(new(worldID, worldName, minPrice));
                    }

                    cachedDCWorldPrices[dcName] = worldPricesList.OrderBy(w => w.WorldName).ToList();
                }

                var allPrices = cachedDCWorldPrices.SelectMany(x => x.Value).ToList();

                if (allPrices.Count > 0)
                {
                    MinPriceData = allPrices.MinBy(x => x.MinPrice);

                    var validPrices = allPrices.Where(x => x.MinPrice != ulong.MaxValue).ToList();
                    MaxPriceData = validPrices.Count > 0 ?
                                       validPrices.MaxBy(x => x.MinPrice) :
                                       default;
                }
                else
                {
                    MinPriceData = default;
                    MaxPriceData = default;
                }

                worldPriceRanks      = BuildWorldPriceRanks();
                worldPriceTableDirty = false;
            }

            return worldPriceRanks;
        }

        private WorldPriceRanks BuildWorldPriceRanks()
        {
            // 需求：勾选「仅显示当前大区数据」时，三低/三高卡片只在当前数据中心（DC）内排名
            var source = cachedDCWorldPrices.AsEnumerable();

            if (owner.config.OnlyCurrentDC)
            {
                var currentDCName = GetCurrentDCName();

                if (!string.IsNullOrEmpty(currentDCName))
                    source = source.Where(dc => dc.Key == currentDCName);
            }

            var validWorldPrices = source
                                   .SelectMany
                                   (dc => dc.Value.Where(world => world.MinPrice != ulong.MaxValue)
                                            .Select(world => new RankedWorldPriceRow(dc.Key, world.WorldID, world.WorldName, world.MinPrice))
                                   )
                                   .OrderBy(x => x.MinPrice)
                                   .ThenBy(x => x.WorldName)
                                   .ToList();

            var cheapestWorlds   = validWorldPrices.Take(Math.Min(3, Math.Max(1, validWorldPrices.Count / 2))).ToList();
            var cheapestWorldIDs = cheapestWorlds.Select(x => x.WorldID).ToHashSet();
            var expensiveWorlds = validWorldPrices.Where(x => !cheapestWorldIDs.Contains(x.WorldID))
                                                  .OrderByDescending(x => x.MinPrice)
                                                  .ThenBy(x => x.WorldName)
                                                  .Take(Math.Min(3, validWorldPrices.Count - cheapestWorldIDs.Count))
                                                  .OrderBy(x => x.MinPrice)
                                                  .ThenBy(x => x.WorldName)
                                                  .ToList();

            if (validWorldPrices.Count == 1)
            {
                expensiveWorlds = [.. validWorldPrices];
                cheapestWorlds.Clear();
            }

            var currentWorldPrice = cachedDCWorldPrices.SelectMany(x => x.Value)
                                                       .FirstOrDefault(x => x.WorldID == GameState.CurrentWorld);

            return new(validWorldPrices, cheapestWorlds, expensiveWorlds, currentWorldPrice);
        }

        /// <summary>玩家当前世界所属的数据中心名；无法判定时返回空字符串。</summary>
        private string GetCurrentDCName()
        {
            if (!owner.allWorlds.TryGetValue(EffectiveRegionName, out var dcsInRegion))
                return string.Empty;

            var currentWorld = GameState.CurrentWorld;

            foreach (var (dcName, worldsInDC) in dcsInRegion)
            {
                if (worldsInDC.ContainsKey(currentWorld))
                    return dcName;
            }

            return string.Empty;
        }

        private bool TryGetOnlineAggregatedMinPrice
        (
            uint      itemID,
            uint      worldID,
            out ulong minPrice
        )
        {
            minPrice = ulong.MaxValue;

            if (!onlineAggregatedCache.TryGetValue((itemID, worldID), out var response))
                return false;

            var result = response.Results.FirstOrDefault(result => result.ItemID == itemID);

            if (result == null)
                return false;

            var price = GetAggregatedMarketScope(result, HQOnly).MinListing.World.Price;

            if (price is not > 0)
                return false;

            minPrice = (ulong)Math.Round(price.Value);
            return true;
        }

        #endregion

        #region 聚合统计与物品来源

        public (float DailySales, ulong? AvgPrice, (ulong Price, DateTime Time, string WorldName)? RecentPurchase) GetItemAggregatedStats
        (
            uint itemID,
            uint targetWorldID,
            bool hqOnly
        )
        {
            float                                           dailySales     = 0;
            ulong?                                          avgPrice       = null;
            (ulong Price, DateTime Time, string WorldName)? recentPurchase = null;

            if (onlineAggregatedCache.TryGetValue((itemID, targetWorldID), out var response))
            {
                var result = response.Results.FirstOrDefault(x => x.ItemID == itemID);

                if (result != null)
                {
                    var scope = GetAggregatedMarketScope(result, hqOnly);

                    dailySales = scope.DailySaleVelocity.World.Quantity ?? scope.DailySaleVelocity.Region.Quantity ?? 0;

                    if (scope.AverageSalePrice.World.Price is > 0)
                        avgPrice = (ulong)Math.Round(scope.AverageSalePrice.World.Price.Value);
                    else if (scope.AverageSalePrice.Region.Price is > 0)
                        avgPrice = (ulong)Math.Round(scope.AverageSalePrice.Region.Price.Value);

                    if (scope.RecentPurchase.World is { Price: > 0, Timestamp: > 0 })
                    {
                        recentPurchase = ((ulong)Math.Round(scope.RecentPurchase.World.Price.Value),
                                             DateTimeOffset.FromUnixTimeMilliseconds(scope.RecentPurchase.World.Timestamp.Value).LocalDateTime,
                                             LuminaWrapper.GetWorldName(targetWorldID));
                    }
                    else if (scope.RecentPurchase.Region is { Price: > 0, Timestamp: > 0 })
                    {
                        var worldName = scope.RecentPurchase.Region.WorldID is > 0 ?
                                            LuminaWrapper.GetWorldName(scope.RecentPurchase.Region.WorldID.Value) :
                                            string.Empty;
                        recentPurchase = ((ulong)Math.Round(scope.RecentPurchase.Region.Price.Value),
                                             DateTimeOffset.FromUnixTimeMilliseconds(scope.RecentPurchase.Region.Timestamp.Value).LocalDateTime,
                                             worldName);
                    }
                }
            }

            return (dailySales, avgPrice, recentPurchase);
        }

        public ulong? GetSelectedWorldMinPrice
        (
            uint itemID,
            bool hqOnly
        )
        {
            if (SelectedWorldID == GameState.CurrentWorld && IsGameMarketDataUsable(itemID))
            {
                var localMinPrice = InfoProxy->Listings.ToArray()
                                                   .Where
                                                   (x => x.ItemId    == itemID &&
                                                         x.UnitPrice > 0       &&
                                                         (x.IsHqItem || !hqOnly))
                                                   .Select(x => x.UnitPrice)
                                                   .DefaultIfEmpty()
                                                   .Min();
                return localMinPrice > 0 ? localMinPrice : null;
            }

            if (onlineAggregatedCache.TryGetValue((itemID, SelectedWorldID), out var response))
            {
                var result = response.Results.FirstOrDefault(x => x.ItemID == itemID);
                var price = result == null ? null : GetAggregatedMarketScope(result, hqOnly).MinListing.World.Price;
                if (price is > 0)
                    return (ulong)Math.Round(price.Value);
            }

            if (!onlineDataCache.TryGetValue((itemID, SelectedWorldID, hqOnly), out var marketData) ||
                !marketData.Items.TryGetValue(itemID, out var itemData))
                return null;

            var onlineMinPrice = itemData.Listings?
                                           .Where(x => x.PricePerUnit > 0 && (x.HQ || !hqOnly))
                                           .Select(x => x.PricePerUnit)
                                           .DefaultIfEmpty()
                                           .Min() ?? 0;
            return onlineMinPrice > 0 ? onlineMinPrice : null;
        }

        public ulong? GetRegionMinPrice
        (
            uint itemID,
            bool hqOnly
        )
        {
            if (!owner.allWorlds.TryGetValue(EffectiveRegionName, out var region))
                return null;

            ulong? minPrice = null;
            foreach (var worldID in region.Values.SelectMany(static worlds => worlds.Keys))
            {
                if (!onlineAggregatedCache.TryGetValue((itemID, worldID), out var response))
                    continue;

                var result = response.Results.FirstOrDefault(x => x.ItemID == itemID);
                var price = result == null ? null : GetAggregatedMarketScope(result, hqOnly).MinListing.World.Price;
                if (price is not > 0)
                    continue;

                var roundedPrice = (ulong)Math.Round(price.Value);
                if (roundedPrice > 0 && (minPrice == null || roundedPrice < minPrice.Value))
                    minPrice = roundedPrice;
            }

            return minPrice;
        }

        public UniversalisAggregatedMarketDataResponse? GetAggregatedResponse
        (
            uint itemID,
            uint worldID
        ) =>
            onlineAggregatedCache.GetValueOrDefault((itemID, worldID));

        public ItemSourceInfo? GetItemSourceInfo
        (
            uint itemID
        )
        {
            if (itemSourceCache.TryGetValue(itemID, out var cached))
                return cached;

            var result = ItemSourceInfo.Query(itemID);
            if (result is not { State: ItemSourceQueryState.Ready, Data: { } sourceInfo })
                return null;

            itemSourceCache[itemID] = sourceInfo;
            return sourceInfo;
        }

        public uint? GetNPCGilPrice
        (
            uint itemID
        )
        {
            if (npcGilPriceCache.TryGetValue(itemID, out var cached))
                return cached;

            var sourceInfo = GetItemSourceInfo(itemID);
            if (sourceInfo == null)
                return null;

            uint? price = null;

            foreach (var npcInfo in sourceInfo.NPCInfos)
            {
                foreach (var costInfo in npcInfo.CostInfos)
                {
                    const uint GIL_ITEM_ID = 1;

                    if (costInfo is not { ItemID: GIL_ITEM_ID, Cost: > 0 })
                        continue;

                    if (price == null || costInfo.Cost < price.Value)
                        price = costInfo.Cost;
                }
            }

            npcGilPriceCache[itemID] = price;
            return price;
        }

        public bool RequestTooltipAggregatedScope
        (
            uint   itemID,
            string scope
        )
        {
            if (string.IsNullOrWhiteSpace(scope))
                return false;

            var cacheKey = (itemID, scope);

            _ = RemoteUniversalisAggregatedMarket.GetOrRequest([itemID], scope);
            if (tooltipAggregatedSubscriptionCache.ContainsKey(cacheKey))
                return false;

            var epoch = itemEpoch;

            tooltipAggregatedSubscriptionCache[cacheKey] = RemoteUniversalisAggregatedMarket.Observe
            (
                [itemID],
                scope,
                snapshot =>
                {
                    if (epoch != itemEpoch)
                        return;

                    if (!snapshot.HasValue)
                        return;

                    TooltipManager.Instance().TriggerItemDetailUpdate();
                }
            );

            return true;
        }

        #endregion
    }
}
