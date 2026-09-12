using System.Numerics;
using DailyRoutines.Common.Info;
using DailyRoutines.Common.Module.Abstractions;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;

namespace FFXIVAutoBuyer.MarketBoard;

public partial class MarketBoardModule
{
    private readonly record struct WorldPriceRow
    (
        uint   WorldID,
        string WorldName,
        ulong  MinPrice
    );

    private readonly record struct RankedWorldPriceRow
    (
        string DCName,
        uint   WorldID,
        string WorldName,
        ulong  MinPrice
    );

    private readonly record struct HistoryEntry
    (
        double   X,
        DateTime SaleTime,
        ulong    PricePerUnit,
        uint     Quantity,
        bool     IsHQ
    );

    private sealed class Config : ModuleConfig
    {
        /// <summary>大区 → 数据中心 → 世界 ID → 世界名（缓存自 Universalis 目录）。</summary>
        public Dictionary<string, Dictionary<string, Dictionary<uint, string>>> AllWorlds = [];

        public Dictionary<uint, MarketFavoriteItem> FavoriteItems = [];

        /// <summary>「仅显示当前大区数据」：三低/三高卡片只在当前数据中心内排名。</summary>
        public bool OnlyCurrentDC = true;

        /// <summary>顶部「购买」的目标持有数量（记忆值）。</summary>
        public uint PurchaseQuantity = 1;

        /// <summary>列表中直接购买的修饰键。</summary>

        public bool AppendMarketStatsTooltip = true;

        /// <summary>是否输出诊断日志（默认关闭；排查问题时可在插件设置中开启）。</summary>
        public bool EnableDiagnostics;

    }

    private enum ItemSelectorTab
    {
        Search,
        Favorite
    }

    private sealed class MarketFavoriteItem
    {
        public uint   ItemID { get; set; }
        public string Note   { get; set; } = string.Empty;

        public Item GetData() =>
            LuminaGetter.GetRow<Item>(ItemID).GetValueOrDefault();
    }
}
