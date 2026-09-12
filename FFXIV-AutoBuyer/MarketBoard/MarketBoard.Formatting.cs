using System.Globalization;

namespace FFXIVAutoBuyer.MarketBoard;

/// <summary>数值显示格式。</summary>
internal static class MarketBoardFormatting
{
    /// <summary>
    /// 金币显示：英文风格千分位（如 <c>1,234,567</c>）。
    /// 按需求不再使用中文单位（万 / 亿）。
    /// </summary>
    public static string ToGilString
    (
        this ulong value
    ) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    /// <inheritdoc cref="ToGilString(ulong)" />
    public static string ToGilString
    (
        this uint value
    ) =>
        ((ulong)value).ToGilString();

    /// <inheritdoc cref="ToGilString(ulong)" />
    public static string ToGilString
    (
        this int value
    ) =>
        value < 0 ?
            value.ToString("N0", CultureInfo.InvariantCulture) :
            ((ulong)value).ToGilString();
}
