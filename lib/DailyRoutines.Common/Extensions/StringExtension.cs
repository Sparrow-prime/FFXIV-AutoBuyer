using DailyRoutines.Common.Info;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;

namespace DailyRoutines.Common.Extensions;

public static class StringExtension
{
    // TODO: 改成 ReadOnlyString
    extension(string text)
    {
        public SeString WithDRPrefix() => 
            new SeStringBuilder()
                .Append(Assets.BoxedLettersDR.ToDalamudString())
                .AddText($" {text}")
                .Build();
    
        public SeString WithDPrefix() => 
            new SeStringBuilder()
                .Append(Assets.BoxedLetterD.ToDalamudString())
                .Append($" {text}")
                .Build();
        
        public SeString WithRPrefix() => 
            new SeStringBuilder()
                .Append(Assets.BoxedLetterR.ToDalamudString())
                .Append($" {text}")
                .Build();
    }
}
