using DailyRoutines.Common.Info;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;

namespace DailyRoutines.Common.Extensions;

public static class SeStringExtension
{
    // TODO: 改成 ReadOnlyString
    extension(SeString text)
    {
        public SeString WithDRPrefix() => 
            new SeStringBuilder()
                .Append(Assets.BoxedLettersDR.ToDalamudString())
                .AddText(" ")
                .Append(text)
                .Build();
    
        public SeString WithDPrefix() => 
            new SeStringBuilder()
                .Append(Assets.BoxedLetterD.ToDalamudString())
                .Append(" ")
                .Append(text)
                .Build();
        
        public SeString WithRPrefix() => 
            new SeStringBuilder()
                .Append(Assets.BoxedLetterR.ToDalamudString())
                .Append(" ")
                .Append(text)
                .Build();
    }
}
