using System.Numerics;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmenTools.OmenService;

namespace FFXIVAutoBuyer.MarketBoard;

public unsafe partial class MarketBoardModule
{
    private void DrawLeftPanel
    (
        MarketBoardUIContext frame
    )
    {
        using (var mainChild = ImRaii.Child("###LeftMainContainer", new(-1), false, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar))
        {
            if (mainChild)
            {
                DrawSidebarSegmentedControl();

                ImGui.Spacing();

                switch (currentTab)
                {
                    case ItemSelectorTab.Favorite:
                        DrawItemSelectorFavorite(frame);
                        break;
                    default:
                        currentTab = ItemSelectorTab.Search;
                        DrawItemSelectorSearch(frame);
                        break;
                }
            }
        }
    }

    private void DrawSidebarSegmentedControl()
    {
        const int TAB_COUNT  = 2;
        var       availWidth = ImGui.GetContentRegionAvail().X;
        var       tabHeight  = (ImGui.GetTextLineHeight() * 1.15f) + (6f * GlobalUIScale);
        var       tabWidth   = availWidth / TAB_COUNT;
        var       rounding   = 4f         * GlobalUIScale;

        var startPos = ImGui.GetCursorScreenPos();
        var maxPos   = startPos + new Vector2(availWidth, tabHeight);
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddRectFilled(startPos, maxPos, ImGui.GetColorU32(ImGuiCol.FrameBg, 0.35f), rounding);
        drawList.AddRect(startPos, maxPos, ImGui.GetColorU32(ImGuiCol.Border,        0.20f), rounding, ImDrawFlags.None, 1f * GlobalUIScale);

        for (var i = 0; i < ItemSelectorTabs.Length; i++)
        {
            var tab        = ItemSelectorTabs[i];
            var isSelected = currentTab == tab;
            var icon = tab switch
            {
                ItemSelectorTab.Favorite => FontAwesomeIcon.Star.ToIconString(),
                _                        => FontAwesomeIcon.Search.ToIconString()
            };
            var label = tab switch
            {
                ItemSelectorTab.Favorite => Lang.Get("Favorite"),
                _                        => Lang.Get("Search")
            };
            var badge = tab == ItemSelectorTab.Favorite ?
                            config.FavoriteItems.Count :
                            0;
            var tabMin = startPos + new Vector2(i * tabWidth, 0);
            var tabMax = tabMin   + new Vector2(tabWidth,     tabHeight);

            ImGui.SetCursorScreenPos(tabMin);

            if (ImGui.InvisibleButton($"###SegmentedTab_{(int)tab}", new(tabWidth, tabHeight)))
            {
                currentTab            = tab;
            }

            var isHovered = ImGui.IsItemHovered();
            if (isHovered)
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            if (isSelected)
            {
                var selectBgColor = KnownColor.LightSkyBlue.ToVector4().WithW
                (
                    isHovered ?
                        0.35f :
                        0.25f
                ).ToUInt();
                var selectBorderColor = KnownColor.LightSkyBlue.ToVector4().WithW(0.85f).ToUInt();

                drawList.AddRectFilled(tabMin, tabMax, selectBgColor, rounding);
                drawList.AddRect(tabMin, tabMax, selectBorderColor, rounding, ImDrawFlags.None, 1f * GlobalUIScale);
            }
            else if (isHovered)
            {
                var hoverBgColor = ImGui.GetColorU32(ImGuiCol.FrameBgHovered, 0.40f);
                drawList.AddRectFilled(tabMin, tabMax, hoverBgColor, rounding);
            }

            var textContent = badge > 0 ?
                                  $"{icon} {label} ({badge})" :
                                  $"{icon} {label}";

            using (UIFont(0.8f).Push())
            {
                var textSize = ImGui.CalcTextSize(textContent);
                var textPos  = new Vector2(tabMin.X + MathF.Max(2f, (tabWidth - textSize.X) / 2f), tabMin.Y + ((tabHeight - textSize.Y) / 2f));
                var textColor = isSelected ? KnownColor.LightSkyBlue.ToUInt() :
                                isHovered  ? ImGui.GetColorU32(ImGuiCol.Text) : ImGui.GetColorU32(ImGuiCol.TextDisabled);

                drawList.PushClipRect(tabMin, tabMax, true);
                drawList.AddText(textPos, textColor, textContent);
                drawList.PopClipRect();
            }
        }

        ImGui.SetCursorScreenPos(startPos + new Vector2(0, tabHeight));
    }

    private void DrawItemSelectorSearch
    (
        MarketBoardUIContext frame
    )
    {
        var inputWidth = ImGui.GetContentRegionAvail().X;
        var hasInput   = !string.IsNullOrEmpty(itemSearchInput);
        var clearBtnW  = ImGui.GetFrameHeight();

        if (hasInput)
            ImGui.SetNextItemWidth(inputWidth - clearBtnW - ImGui.GetStyle().ItemSpacing.X);
        else
            ImGui.SetNextItemWidth(inputWidth);

        if (ImGui.InputTextWithHint("###ItemSearchInput", Lang.Get("PleaseSearch"), ref itemSearchInput, 256))
            ExecuteOverlaySearch(itemSearchInput);

        if (hasInput)
        {
            ImGui.SameLine();
            if (ImGui.Button("×###ClearSearchInput", new(clearBtnW, 0)))
                ExecuteOverlaySearch(string.Empty);
            ImGuiOm.TooltipHover(Lang.Get("Clear"));
        }

        ImGui.Spacing();

        using var child = ImRaii.Child("###SearchListContainer", new(-1, -1), false, ImGuiWindowFlags.NoBackground);
        if (!child) return;

        if (!string.IsNullOrWhiteSpace(itemSearchInput))
        {
            var groupedData = provider.GetSearchGroups(itemSearchInput);

            if (groupedData.Count == 0)
            {
                DrawEmptyState(FontAwesomeIcon.Search.ToIconString(), LuminaWrapper.GetAddonText(2717));
                return;
            }

            foreach (var (category, items) in groupedData)
            {
                var categoryID   = category.RowId;
                var categoryName = category.Name.ToString();
                if (!ITextureProvider.Instance().TryGetFromGameIcon(new((uint)category.Icon), out var texture)) continue;

                if (ImGuiOm.TreeNodeImageWithText
                    (
                        texture.GetWrapOrEmpty().Handle,
                        new(ImGui.GetTextLineHeight()),
                        $"{categoryName}##{categoryID}-Search",
                        ImGuiTreeNodeFlags.DefaultOpen
                    ))
                {
                    var clipper = new ImGuiListClipper();
                    clipper.Begin(items.Count);

                    while (clipper.Step())
                    {
                        for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                            RenderItemCard(frame, items[i]);
                    }

                    ImGui.TreePop();
                }
            }
        }
        else
        {
            foreach (var searchCategory in ValidCategories)
            {
                var name = searchCategory.Name.ToString();
                if (string.IsNullOrEmpty(name)) continue;
                if (!searchCategoryToItems.TryGetValue(searchCategory.RowId, out var data) || data.Count == 0) continue;
                if (!ITextureProvider.Instance().TryGetFromGameIcon(new((uint)searchCategory.Icon), out var texture)) continue;

                if (ImGuiOm.TreeNodeImageWithText
                    (
                        texture.GetWrapOrEmpty().Handle,
                        new(ImGui.GetTextLineHeight()),
                        $"{name}##{searchCategory.RowId}-Default"
                    ))
                {
                    var clipper = new ImGuiListClipper();
                    clipper.Begin(data.Count);

                    while (clipper.Step())
                    {
                        for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                            RenderItemCard(frame, data[i]);
                    }

                    ImGui.TreePop();
                }
            }
        }
    }

    private void DrawItemSelectorFavorite
    (
        MarketBoardUIContext frame
    )
    {
        using var child = ImRaii.Child("###FavoriteContainer", new(-1, -1), false, ImGuiWindowFlags.NoBackground);
        if (!child) return;

        if (config.FavoriteItems.Count == 0)
        {
            DrawEmptyState(FontAwesomeIcon.Star.ToIconString(), LuminaWrapper.GetAddonText(2717));
            return;
        }

        var favoriteList = favoriteItemsCache;

        if (favoriteList == null || favoriteItemsCacheVersion != favoriteItemsVersion)
        {
            favoriteItemsCache        = favoriteList = config.FavoriteItems.Values.ToList();
            favoriteItemsCacheVersion = favoriteItemsVersion;
        }

        var favoriteClipper = new ImGuiListClipper();
        favoriteClipper.Begin(favoriteList.Count);

        while (favoriteClipper.Step())
        {
            for (var i = favoriteClipper.DisplayStart; i < favoriteClipper.DisplayEnd; i++)
            {
                var favoriteItem = favoriteList[i];
                var itemData     = favoriteItem.GetData();
                if (itemData.RowId == 0) continue;

                RenderItemCard(frame, itemData, note: favoriteItem.Note);
            }
        }
    }

    private void RenderItemCard
    (
        MarketBoardUIContext frame,
        Item                 item,
        DateTime?            accessTime = null,
        string?              note       = null
    )
    {
        var isCurrentHQ = provider.SelectedItemID == item.RowId && provider.HQOnly;
        if (!ITextureProvider.Instance().TryGetFromGameIcon(new(item.Icon, isCurrentHQ), out var texture)) return;

        using var id = ImRaii.PushId($"{item.RowId}_{currentTab}");

        var isSelected = provider.SelectedItemID == item.RowId;
        var isFavorite = config.FavoriteItems.ContainsKey(item.RowId);

        var availWidth = ImGui.GetContentRegionAvail().X;

        // 卡片高度：容纳「物品名 + 品级」两行文字，并给右上角收藏按钮留出空间
        // （字号档位上调后原高度不足以容纳，出现收藏/品级上下出界）
        var rowHeight   = (ImGui.GetTextLineHeight() * 2f) + (10f * GlobalUIScale);
        var actionAreaW = 20f * GlobalUIScale;
        var padX        = 4f  * GlobalUIScale;
        var mainButtonW = availWidth - actionAreaW - (4f * GlobalUIScale);
        var cardSize    = new Vector2(availWidth, rowHeight);
        var rounding    = 4f * GlobalUIScale;

        var startPos = ImGui.GetCursorScreenPos();
        var maxPos   = startPos + cardSize;
        var drawList = ImGui.GetWindowDrawList();

        ImGui.SetCursorScreenPos(startPos);

        ImGui.InvisibleButton($"###CardButton_{item.RowId}", new(mainButtonW, rowHeight));

        var isHovered = ImGui.IsItemHovered();
        if (isHovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        if (isHovered)
        {
            ImGuiOm.TooltipHover(item.Name.ToString());
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            provider.SelectItem(item.RowId, hqOnly: isCurrentHQ, reason: "点击物品卡片");

        Vector4 bgColor;
        Vector4 borderColor;

        if (isSelected)
        {
            bgColor = KnownColor.LightSkyBlue.ToVector4().WithW
            (
                isHovered ?
                    0.30f :
                    0.20f
            );
            borderColor = KnownColor.LightSkyBlue.ToVector4().WithW(0.85f);
        }
        else if (isHovered)
        {
            bgColor     = ImGui.GetColorU32(ImGuiCol.FrameBgHovered, 0.40f).ToVector4();
            borderColor = ImGui.GetColorU32(ImGuiCol.Border,         0.35f).ToVector4();
        }
        else
        {
            bgColor     = ImGui.GetColorU32(ImGuiCol.FrameBg, 0.22f).ToVector4();
            borderColor = ImGui.GetColorU32(ImGuiCol.Border,  0.12f).ToVector4();
        }

        drawList.AddRectFilled(startPos, maxPos, bgColor.ToUInt(), rounding);
        drawList.AddRect(startPos, maxPos, borderColor.ToUInt(), rounding, ImDrawFlags.None, 1f * GlobalUIScale);

        var iconSize = rowHeight - (6f * GlobalUIScale);
        var iconPos  = new Vector2(startPos.X + padX, startPos.Y + (3f * GlobalUIScale));
        drawList.AddImage(texture.GetWrapOrEmpty().Handle, iconPos, iconPos + new Vector2(iconSize));

        var textStartX = iconPos.X                + iconSize + (6f * GlobalUIScale);
        var textMaxX   = startPos.X + mainButtonW - padX;

        var timeWidth = 0f;

        if (accessTime != null)
        {
            var timeText = $"{accessTime.Value:HH:mm}";

            using (UIFont(0.8f).Push())
            {
                var timeSize = ImGui.CalcTextSize(timeText);
                timeWidth = timeSize.X + (4f * GlobalUIScale);
                var timePos = new Vector2(textMaxX - timeSize.X, startPos.Y + (4f * GlobalUIScale));
                drawList.AddText(timePos, ImGui.GetColorU32(ImGuiCol.TextDisabled), timeText);
            }
        }

        var contentMaxX = textMaxX - timeWidth;

        var itemName = item.Name.ToString();
        if (isCurrentHQ)
            itemName += " \ue03c";

        var namePos = new Vector2(textStartX, startPos.Y + (3f * GlobalUIScale));

        drawList.PushClipRect(startPos, maxPos with { X = contentMaxX }, true);
        drawList.AddText
        (
            namePos,
            isSelected ?
                KnownColor.LightSkyBlue.ToUInt() :
                ImGui.GetColorU32(ImGuiCol.Text),
            itemName
        );
        drawList.PopClipRect();

        var badgeY    = startPos.Y + ImGui.GetTextLineHeight() + (2f * GlobalUIScale);
        var curBadgeX = textStartX;

        using (UIFont(0.8f).Push())
        {
            var ilvlText = $"\ue033 {item.LevelItem.RowId}";
            curBadgeX = DrawItemCardBadge
            (
                drawList,
                ilvlText,
                curBadgeX,
                badgeY,
                KnownColor.LightSkyBlue.ToVector4().WithW(0.15f).ToUInt(),
                KnownColor.LightSkyBlue.ToUInt()
            );

            if (item.CanBeHq)
            {
                const string HQ_TEXT = "\ue03c";

                var hqBg = isCurrentHQ ?
                               KnownColor.LightSkyBlue.ToVector4().WithW(0.25f).ToUInt() :
                               ImGui.GetColorU32(ImGuiCol.FrameBg, 0.40f);
                var hqColor = isCurrentHQ ?
                                  KnownColor.LightSkyBlue.ToUInt() :
                                  KnownColor.Gray.ToUInt();

                curBadgeX = DrawItemCardBadge(drawList, HQ_TEXT, curBadgeX, badgeY, hqBg, hqColor);
            }

            if (item.ItemSearchCategory.Value.RowId > 0)
            {
                var catText = item.ItemSearchCategory.Value.Name.ToString();
                curBadgeX = DrawItemCardBadge
                (
                    drawList,
                    catText,
                    curBadgeX,
                    badgeY,
                    ImGui.GetColorU32(ImGuiCol.FrameBg, 0.40f),
                    ImGui.GetColorU32(ImGuiCol.TextDisabled),
                    contentMaxX,
                    true
                );
            }

            if (!string.IsNullOrEmpty(note))
            {
                var noteText = $"✎ {note}";

                if (curBadgeX < contentMaxX)
                {
                    drawList.PushClipRect(startPos, new Vector2(contentMaxX, maxPos.Y), true);
                    DrawItemCardBadge
                    (
                        drawList,
                        noteText,
                        curBadgeX,
                        badgeY,
                        KnownColor.DarkOrange.ToVector4().WithW(0.18f).ToUInt(),
                        KnownColor.Orange.ToUInt()
                    );
                    drawList.PopClipRect();
                }
            }
        }

        var actionX = startPos.X + mainButtonW + (2f * GlobalUIScale);
        var halfH   = rowHeight / 2f;

        var favBtnPos  = new Vector2(actionX,     startPos.Y + (1f * GlobalUIScale));
        var favBtnSize = new Vector2(actionAreaW, halfH      - (2f * GlobalUIScale));
        ImGui.SetCursorScreenPos(favBtnPos);

        if (ImGui.InvisibleButton($"###FavBtn_{item.RowId}", favBtnSize))
        {
            if (isFavorite)
                config.FavoriteItems.Remove(item.RowId);
            else
                config.FavoriteItems[item.RowId] = new() { ItemID = item.RowId, Note = string.Empty };

            favoriteItemsVersion++;
            SaveConfig(config);
        }

        var isFavHovered = ImGui.IsItemHovered();

        if (isFavHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGuiOm.TooltipHover
            (
                isFavorite ?
                    Lang.Get("Unfavorite") :
                    Lang.Get("Favorite")
            );
        }

        var starIcon = FontAwesomeIcon.Star.ToIconString();
        var starColor = isFavorite   ? KnownColor.Goldenrod.ToUInt() :
                        isFavHovered ? ImGui.GetColorU32(ImGuiCol.Text) : ImGui.GetColorU32(ImGuiCol.TextDisabled, 0.40f);

        using (UIFont(0.8f).Push())
        {
            var starSize = ImGui.CalcTextSize(starIcon);
            var starPos  = new Vector2(favBtnPos.X + ((favBtnSize.X - starSize.X) / 2f), favBtnPos.Y + ((favBtnSize.Y - starSize.Y) / 2f));
            drawList.AddText(starPos, starColor, starIcon);
        }


        ImGui.SetCursorScreenPos(startPos + new Vector2(0, rowHeight + ImGui.GetStyle().ItemSpacing.Y));
    }

    private static float DrawItemCardBadge
    (
        ImDrawListPtr drawList,
        string        text,
        float         x,
        float         y,
        uint          backgroundColor,
        uint          textColor,
        float         maxX       = float.PositiveInfinity,
        bool          requireFit = false
    )
    {
        var textSize = ImGui.CalcTextSize(text);
        var paddingX = 3f * GlobalUIScale;
        var min      = new Vector2(x,                                y);
        var max      = new Vector2(x + textSize.X + (paddingX * 2f), y + textSize.Y + (2f * GlobalUIScale));

        if (requireFit ?
                max.X >= maxX :
                min.X >= maxX)
            return x;

        drawList.AddRectFilled(min, max, backgroundColor, 2f * GlobalUIScale);
        drawList.AddText(new Vector2(min.X + paddingX, min.Y + (1f * GlobalUIScale)), textColor, text);
        return max.X + (4f * GlobalUIScale);
    }

    private static void DrawEmptyState
    (
        string icon,
        string message
    )
    {
        var availSize = ImGui.GetContentRegionAvail();
        var centerPos = ImGui.GetCursorScreenPos() + (availSize / 2f);

        using (UIFont(1.6f).Push())
        {
            var iconSize = ImGui.CalcTextSize(icon);
            var iconPos  = new Vector2(centerPos.X - (iconSize.X / 2f), centerPos.Y - iconSize.Y - (6f * GlobalUIScale));
            ImGui.GetWindowDrawList().AddText(iconPos, ImGui.GetColorU32(ImGuiCol.TextDisabled, 0.40f), icon);
        }

        using (UIFont(0.8f).Push())
        {
            var msgSize = ImGui.CalcTextSize(message);
            var msgPos  = new Vector2(centerPos.X - (msgSize.X / 2f), centerPos.Y + (6f * GlobalUIScale));
            ImGui.GetWindowDrawList().AddText(msgPos, ImGui.GetColorU32(ImGuiCol.TextDisabled, 0.60f), message);
        }
    }

}
