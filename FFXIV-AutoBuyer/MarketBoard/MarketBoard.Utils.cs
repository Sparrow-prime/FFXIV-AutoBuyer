using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmenTools.OmenService;

namespace FFXIVAutoBuyer.MarketBoard;

public unsafe partial class MarketBoardModule
{
    #region 跨界传送日志判定（「使用跨界传送移动到了xxx。」）

    /// <summary>
    /// 跨界传送日志行的候选 LogMessage 行号：模板含固定文案的行。
    /// 作为廉价快路径用于过滤（未命中模板的行不必再解析文本）。
    /// </summary>
    private readonly HashSet<uint> worldVisitLogMessageIDs = [];

    /// <summary>
    /// 是否处于「跨界传送监测」状态：**插件窗口未打开时不监测**（与通信最小化原则一致）。
    /// 窗口关闭期间发生的跨服由开窗时的世界校正兜底（见 <c>SyncWorldOnWindowOpen</c>）。
    /// </summary>
    private bool IsWorldVisitMonitoringActive() =>
        Overlay is { IsOpen: true };

    /// <summary>
    /// 反查跨界传送日志行号：LogMessage 表中模板含「使用跨界传送移动到了」的行。
    /// 找不到时（例如表结构变化）退化为纯文本匹配。
    /// </summary>
    private void BuildWorldVisitLogMessageIDs()
    {
        worldVisitLogMessageIDs.Clear();

        foreach (var row in LuminaGetter.Get<LogMessage>())
        {
            if (row.Text.ToString().Contains(WORLD_VISIT_LOG_PREFIX, StringComparison.Ordinal))
                worldVisitLogMessageIDs.Add(row.RowId);
        }

        MarketDataProvider.DiagLog
        (
            worldVisitLogMessageIDs.Count > 0
                ? $"跨服判定：命中 {worldVisitLogMessageIDs.Count} 条跨界传送日志模板（{string.Join(", ", worldVisitLogMessageIDs)}）"
                : "跨服判定：LogMessage 表中未找到跨界传送模板，改为按文本匹配"
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
    /// </summary>
    private uint ResolveWorldIDByName
    (
        string worldName
    )
    {
        foreach (var world in LuminaGetter.Get<World>())
        {
            if (string.Equals(world.Name.ToString(), worldName, StringComparison.Ordinal))
                return world.RowId;
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
            return bestWorldID;

        foreach (var region in allWorlds.Values)
        foreach (var dataCenter in region.Values)
        foreach (var (worldID, name) in dataCenter)
        {
            if (string.Equals(name, worldName, StringComparison.Ordinal))
                return worldID;
        }

        return 0;
    }

    /// <summary>
    /// 「玩家实际所在世界」：优先角色结构体（落地后即更新），为 0 时退回大厅数据。
    /// 仅在世界名解析失败、或开窗校正时使用。
    /// </summary>
    private static uint ResolvePlayerWorldID()
    {
        var worldID = DService.Instance().ObjectTable.LocalPlayer?.CurrentWorld.RowId ?? 0;

        return worldID != 0 ? worldID : GameState.CurrentWorld;
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
