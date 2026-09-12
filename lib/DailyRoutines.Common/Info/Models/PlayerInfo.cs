using DailyRoutines.Common.Info.Enums;
using Dalamud.Game.Gui.PartyFinder.Types;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;

namespace DailyRoutines.Common.Info.Models;

public sealed class PlayerInfo
{
    public PlayerInfo(InfoProxyCommonList.CharacterData data)
    {
        Source     = PlayerInfoSource.InfoProxyCommonList;
        SourceType = typeof(InfoProxyCommonList.CharacterData);
        RawData    = data;

        Name             = data.NameString;
        ContentID        = data.ContentId;
        Job              = data.Job;
        JobData          = LuminaGetter.GetRow<ClassJob>(Job).GetValueOrDefault();
        CurrentWorld     = data.CurrentWorld;
        CurrentWorldData = LuminaGetter.GetRow<World>(data.CurrentWorld).GetValueOrDefault();
        HomeWorld        = data.HomeWorld;
        HomeWorldData    = LuminaGetter.GetRow<World>(HomeWorld).GetValueOrDefault();

        FreeCompanyName = data.FCTagString;
    }

    public PlayerInfo(IPartyFinderListing data)
    {
        Source     = PlayerInfoSource.PartyFinder;
        SourceType = typeof(IPartyFinderListing);
        RawData    = data;

        Name             = data.Name.ToString();
        ContentID        = data.ContentId;
        Job              = data.RawJobsPresent.First();
        JobData          = LuminaGetter.GetRow<ClassJob>(Job).GetValueOrDefault();
        CurrentWorld     = (ushort)data.CurrentWorld.RowId;
        CurrentWorldData = data.CurrentWorld.Value;
        HomeWorld        = (ushort)data.HomeWorld.RowId;
        HomeWorldData    = data.HomeWorld.Value;
    }

    public string   Name             { get; init; }
    public ulong    ContentID        { get; init; }
    public byte     Job              { get; init; }
    public ClassJob JobData          { get; init; }
    public ushort   CurrentWorld     { get; init; }
    public World    CurrentWorldData { get; init; }
    public ushort   HomeWorld        { get; init; }
    public World    HomeWorldData    { get; init; }

    public string FreeCompanyName { get; init; } = string.Empty;

    public PlayerInfoSource Source     { get; init; }
    public Type             SourceType { get; init; }
    public object           RawData    { get; init; }

    public T ConvertFrom<T>()
    {
        if (RawData is T typedData) return typedData;

        throw new InvalidCastException($"无法将原始数据 {RawData.GetType().Name} 转换为目标类型 {typeof(T).Name}");
    }
}
