using DailyRoutines.Common.Interface.ImGuiDR;

namespace DailyRoutines.Common.Extensions;

public static class ImRaiiExtension
{
    extension(ImRaii)
    {
        public static Heading1 Heading1
        (
            string  text,
            string? help = null
        ) =>
            new(text, help);

        public static Heading2 Heading2
        (
            string  text,
            string? help = null
        ) =>
            new(text, help);
    }
}
