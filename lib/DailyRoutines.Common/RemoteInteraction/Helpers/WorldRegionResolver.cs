using DailyRoutines.Common.RemoteInteraction.Enums;
using Lumina.Data;

namespace DailyRoutines.Common.RemoteInteraction.Helpers;

public static class WorldRegionResolver
{
    public static WorldRegion Resolve(uint worldID) =>
        worldID switch
        {
            < 1000 => WorldRegion.GL,
            < 2000 => WorldRegion.CN,
            < 3000 => WorldRegion.KR,
            < 4000 => WorldRegion.TC,
            _      => WorldRegion.None
        };

    public static WorldRegion Resolve(Language language) =>
        language switch
        {
            Language.ChineseSimplified or Language.ChineseTraditional => WorldRegion.CN,
            Language.Korean                                           => WorldRegion.KR,
            Language.TraditionalChinese                               => WorldRegion.TC,
            _                                                         => WorldRegion.GL
        };
}
