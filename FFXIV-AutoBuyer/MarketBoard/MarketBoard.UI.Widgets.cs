using System.Numerics;
using Dalamud.Utility.Numerics;
using OmenTools.OmenService;

namespace FFXIVAutoBuyer.MarketBoard;

public partial class MarketBoardModule
{
    /// <summary>绘制价格文本。按需求不再提供「点击复制价格」。</summary>
    private static void DrawMarketPrice
    (
        ulong price
    ) =>
        ImGui.TextUnformatted($"{price.ToGilString()}\ue049");

    private sealed class MarketBenchmarkInfo
    {
        public ulong   Price;
        public string  Badge = string.Empty;
        public Vector4 Color;
        public string? Tooltip;
        public Action? OnClick;
        public bool    Drawn;
    }

    /// <summary>
    /// 列表中的基准分隔行。按需求已移除「成交均价」，仅保留「NPC 收购价」。
    /// </summary>
    private static List<MarketBenchmarkInfo> BuildMarketBenchmarks
    (
        uint? npcGilPrice
    )
    {
        var list = new List<MarketBenchmarkInfo>();

        if (npcGilPrice is > 0)
        {
            var npcPriceText = $"{npcGilPrice.Value.ToGilString()}\ue049";

            list.Add
            (
                new()
                {
                    Price   = npcGilPrice.Value,
                    Badge   = Lang.Get("BetterMarketBoard-NPCPrice", npcPriceText),
                    Color   = KnownColor.OrangeRed.ToVector4(),
                    Tooltip = Lang.Get("BetterMarketBoard-NPCPrice", npcPriceText)
                }
            );
        }

        return list;
    }

    private static void DrawBenchmarkSeparatorRow
    (
        string  id,
        string  badgeLabel,
        Vector4 accentColor,
        string? tooltip = null,
        Action? onClick = null
    )
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        var rowHeight = (ImGui.GetTextLineHeight() * 1.15f) + (6f * GlobalUIScale);

        var flags = ImGuiSelectableFlags.SpanAllColumns;
        if (onClick == null)
            flags |= ImGuiSelectableFlags.Disabled;

        if (ImGui.Selectable($"###{id}", false, flags, new Vector2(0, rowHeight)) && onClick != null)
            onClick.Invoke();

        var isHovered = ImGui.IsItemHovered();
        if (isHovered && onClick != null)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var min      = ImGui.GetItemRectMin();
        var max      = ImGui.GetItemRectMax();
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 3f * GlobalUIScale;

        var parentClipMin = drawList.GetClipRectMin();
        var parentClipMax = drawList.GetClipRectMax();

        var clipMin = min with { Y = MathF.Max(min.Y, parentClipMin.Y) };
        var clipMax = max with { Y = MathF.Min(max.Y, parentClipMax.Y) };

        if (clipMin.Y >= clipMax.Y || clipMin.X >= clipMax.X)
            return;

        drawList.PushClipRect(clipMin, clipMax, false);

        var bgAlpha = isHovered && onClick != null ?
                          0.16f :
                          0.08f;
        var bgCol = accentColor.WithW(bgAlpha).ToUInt();
        drawList.AddRectFilled(min, max, bgCol, rounding);

        var centerY = min.Y + ((max.Y - min.Y) / 2f);
        var padX    = 8f * GlobalUIScale;

        using (UIFont(0.8f).Push())
        {
            var badgeSize = ImGui.CalcTextSize(badgeLabel);
            var badgePadX = 6f * GlobalUIScale;
            var badgePadY = 2f * GlobalUIScale;

            var badgeMin = new Vector2(min.X      + padX, centerY - (badgeSize.Y / 2f)          - badgePadY);
            var badgeMax = new Vector2(badgeMin.X + badgeSize.X   + (badgePadX   * 2f), centerY + (badgeSize.Y / 2f) + badgePadY);

            var badgeBgAlpha = isHovered && onClick != null ?
                                   0.35f :
                                   0.22f;
            var badgeBorderAlpha = isHovered && onClick != null ?
                                       1.0f :
                                       0.75f;

            drawList.AddRectFilled(badgeMin, badgeMax, accentColor.WithW(badgeBgAlpha).ToUInt(), rounding);
            drawList.AddRect(badgeMin, badgeMax, accentColor.WithW(badgeBorderAlpha).ToUInt(), rounding, ImDrawFlags.None, 1f * GlobalUIScale);
            drawList.AddText(new Vector2(badgeMin.X + badgePadX, centerY - (badgeSize.Y / 2f)), accentColor.ToUInt(), badgeLabel);

            var lineStartX = badgeMax.X + (6f * GlobalUIScale);
            var lineEndX   = max.X      - padX;

            if (lineEndX > lineStartX)
            {
                var lineAlpha = isHovered && onClick != null ?
                                    0.6f :
                                    0.35f;
                drawList.AddLine
                (
                    new Vector2(lineStartX, centerY),
                    new Vector2(lineEndX,   centerY),
                    accentColor.WithW(lineAlpha).ToUInt(),
                    1.5f * GlobalUIScale
                );
            }
        }

        drawList.PopClipRect();

        if (!string.IsNullOrEmpty(tooltip) && isHovered)
            ImGui.SetTooltip(tooltip);
    }
}
