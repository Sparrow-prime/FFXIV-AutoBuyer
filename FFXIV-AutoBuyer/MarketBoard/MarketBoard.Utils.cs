using Dalamud.Game.ClientState.Conditions;
using FFXIVAutoBuyer.Universalis;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmenTools.OmenService;

namespace FFXIVAutoBuyer.MarketBoard;

public unsafe partial class MarketBoardModule
{
    /// <summary>
    /// 取聚合行情中的 NQ / HQ 分档（跨世界价格卡片、卡片统计与均价计算共用）。
    /// </summary>
    private static UniversalisAggregatedMarketScope GetAggregatedMarketScope
    (
        UniversalisAggregatedMarketResult result,
        bool                              hqOnly
    ) =>
        hqOnly ?
            result.HQ :
            result.NQ;

    #region 后台静默（窗口未打开时什么都不做）

    /// <summary>
    /// 插件窗口是否打开。**窗口未打开时插件不做任何自动工作**：
    /// <list type="bullet">
    /// <item>不请求游戏布告板（不发起 / 不补拉本地市场搜索）；</item>
    /// <item>不请求 Universalis（跨世界价格、世界目录）；</item>
    /// <item>不处理跨界传送日志（开窗时由世界校正兜底）。</item>
    /// </list>
    /// 与游戏窗口是否在前台无关；窗口关闭期间发生的变化由**开窗时**一次性校正 + 一次刷新补齐。
    /// <para>
    /// 注意：道具工具提示上的市场数据已整体移除（且 <c>TooltipManager</c> 在插件初始化时即被禁用），
    /// 因此这里不再涉及工具提示。
    /// </para>
    /// </summary>
    private bool IsPluginWindowOpen =>
        Overlay is { IsOpen: true };

    #endregion

    #region 跨界传送日志判定（「使用跨界传送移动到了xxx。」）

    /// <summary>
    /// 跨界传送日志行的候选 LogMessage 行号：模板含固定文案的行。
    /// 作为廉价快路径用于过滤（未命中模板的行不必再解析文本）。
    /// </summary>
    private readonly HashSet<uint> worldVisitLogMessageIDs = [];

    /// <summary>
    /// 是否处于「跨界传送监测」状态：**插件窗口未打开时不监测**（与「后台静默」「通信最小化」一致）。
    /// 未监测期间发生的跨服由开窗时的世界校正兜底（见 <c>SyncWorldOnWindowOpen</c>）。
    /// </summary>
    private bool IsWorldVisitMonitoringActive() =>
        IsPluginWindowOpen;

    /// <summary>
    /// 反查跨界传送日志行号：LogMessage 表中模板含「使用跨界传送移动到了」的行。
    /// 找不到时（例如表结构变化）退化为纯文本匹配。
    /// <para>
    /// 结果**始终**写入日志（见 <see cref="MarketDataProvider.WorldLog"/>），
    /// 用于回答「跨服检测到底有没有读到日志」：若这里报「命中 N 条」，
    /// 说明模板反查可用；若报「未找到」，则检测正在走纯文本匹配。
    /// </para>
    /// </summary>
    private void BuildWorldVisitLogMessageIDs()
    {
        worldVisitLogMessageIDs.Clear();

        foreach (var row in LuminaGetter.Get<LogMessage>())
        {
            if (row.Text.ToString().Contains(WORLD_VISIT_LOG_PREFIX, StringComparison.Ordinal))
                worldVisitLogMessageIDs.Add(row.RowId);
        }

        MarketDataProvider.WorldLog
        (
            worldVisitLogMessageIDs.Count > 0
                ? $"启动：LogMessage 表命中 {worldVisitLogMessageIDs.Count} 条跨界传送模板（行号 {string.Join(", ", worldVisitLogMessageIDs)}），"
                  + $"按行号过滤；当前世界锚点 = {CurrentWorldID}"
                : $"启动：LogMessage 表中未找到含「{WORLD_VISIT_LOG_PREFIX}」的模板，退化为按文本匹配；当前世界锚点 = {CurrentWorldID}"
        );
    }

    /// <summary>
    /// 从日志文本中解析跨界传送的目标世界名。
    /// 文案固定为「使用跨界传送移动到了xxx。」，除服务器名外没有其它变体，
    /// 因此只按固定前缀定位并去掉句号（允许尾部有 SeString 残留字符，故只做 Trim 而不做定长截取）。
    /// </summary>
    private static bool TryParseWorldVisitWorldName
    (
        string  text,
        out string worldName
    )
    {
        worldName = string.Empty;

        var index = text.IndexOf(WORLD_VISIT_LOG_PREFIX, StringComparison.Ordinal);
        if (index < 0) return false;

        var name = text[(index + WORLD_VISIT_LOG_PREFIX.Length)..].Trim();
        name = name.TrimEnd('。', '.', ' ', '\u3000');
        if (name.Length == 0) return false;

        worldName = name;
        return true;
    }

    /// <summary>
    /// 世界名 → 世界 ID：以 Lumina 世界表（当前客户端语言）为准，回退到 Universalis 世界目录。
    /// 先精确匹配；再退化为「候选名以某个世界名为前缀」的最长匹配，
    /// 以容忍渲染文本尾部的 SeString 残留字符。
    /// <paramref name="source"/> 回报实际命中的来源（用于日志定位，见 <c>OnWorldVisitLogMessage</c>）。
    /// </summary>
    private uint ResolveWorldIDByName
    (
        string    worldName,
        out string source
    )
    {
        foreach (var world in LuminaGetter.Get<World>())
        {
            if (string.Equals(world.Name.ToString(), worldName, StringComparison.Ordinal))
            {
                source = "Lumina 世界表（精确匹配）";
                return world.RowId;
            }
        }

        var bestWorldID  = 0U;
        var bestNameLength = 0;

        foreach (var world in LuminaGetter.Get<World>())
        {
            var name = world.Name.ToString();

            if (name.Length > bestNameLength && worldName.StartsWith(name, StringComparison.Ordinal))
            {
                bestWorldID    = world.RowId;
                bestNameLength = name.Length;
            }
        }

        if (bestWorldID != 0)
        {
            source = "Lumina 世界表（前缀匹配，容忍尾部残留字符）";
            return bestWorldID;
        }

        foreach (var region in allWorlds.Values)
        foreach (var dataCenter in region.Values)
        foreach (var (worldID, name) in dataCenter)
        {
            if (string.Equals(name, worldName, StringComparison.Ordinal))
            {
                source = "Universalis 世界目录";
                return worldID;
            }
        }

        source = "未识别";
        return 0;
    }

    /// <summary>
    /// 「玩家实际所在世界」：优先角色结构体（落地后即更新），为 0 时退回大厅数据。
    /// 仅在世界名解析失败、或开窗校正时使用。
    /// <paramref name="source"/> 回报实际取值的来源（用于日志定位）。
    /// </summary>
    private static uint ResolvePlayerWorldID
    (
        out string source
    )
    {
        var worldID = DService.Instance().ObjectTable.LocalPlayer?.CurrentWorld.RowId ?? 0;

        if (worldID != 0)
        {
            source = "角色结构体";
            return worldID;
        }

        source = "大厅数据";
        return GameState.CurrentWorld;
    }

    #endregion

    /// <summary>
    /// 玩家是否处于「过场 / 加载中」（例如跨服传送、进入副本）。
    /// 注意：跨服期间 <c>GameState.CurrentWorld</c> 取自 LobbyData，会在过场**开始**时就变化，
    /// 但此时客户端仍连着原服务器 —— 若在此刻作废并重新获取，拿回来的还是原服务器的数据。
    /// </summary>
    private static bool IsPlayerTransitioning =>
        DService.Instance().Condition.IsBetweenAreas ||
        DService.Instance().ClientState.TerritoryType == 0;

    private static bool IsAbleToSearchLocalMarket() =>
        GameState.IsLoggedIn &&
        GameState.ContentFinderCondition == 0;

    private static bool IsOwnRetainer
    (
        ulong retainerID
    )
    {
        var manager = RetainerManager.Instance();

        if (manager == null) return false;

        for (var i = 0U; i < manager->GetRetainerCount(); i++)
        {
            var retainer = manager->GetRetainerBySortedIndex(i);
            if (retainer != null && retainer->RetainerId == retainerID)
                return true;
        }

        return false;
    }
}
