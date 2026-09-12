using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using OmenTools.OmenService;

namespace FFXIVAutoBuyer.MarketBoard;

public unsafe partial class MarketBoardModule
{
    private static bool IsAbleToSearchMarket() =>
        IsAbleToSearchLocalMarket() &&
        (!ICondition.Instance().IsOccupiedInEvent ||
         ICondition.Instance()[ConditionFlag.OccupiedSummoningBell]) &&
        !ItemSearchResult->IsAddonAndNodesReady();

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
