using KamiToolKit.Nodes;

namespace DailyRoutines.Common.Extensions;

public static class TextNodeExtension
{
    extension(TextNode textNode)
    {
        public void AutoAdjustTextSize()
        {
            while (textNode.FontSize > 1 && textNode.GetTextDrawSize(textNode.String, false).X > textNode.Size.X)
                textNode.FontSize--;
        }
    }
}
