using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using OmenTools.Interop.Game.Lumina;
using OmenTools.OmenService;

namespace FFXIVAutoBuyer.MarketBoard;

public unsafe partial class MarketBoardModule
{
    private void DrawRightContent
    (
        MarketBoardUIContext frame
    )
    {
        var info = InfoProxy;
        if (info == null || frame.ItemID == 0) return;
        if (!frame.HasItem) return;

        var itemData = frame.ItemData;

        var itemIcon = ITextureProvider.Instance().GetFromGameIcon(new(itemData.Icon, frame.HQOnly)).GetWrapOrDefault();
        if (itemIcon == null) return;

        using var font = UIFont(1f).Push();

        var style = ImGui.GetStyle();

        // 图标高度 =「物品名行 + 收藏/刷新按钮行」整体高度：
        // 物品名行 = 1.6 倍字号 + 上下内边距（与带边框控件对齐，至少一个控件高）；
        // 按钮行 = 一个控件高；两行之间留一个行间距。
        var nameBandHeight = MathF.Max
        (
            UIFontSize(1.6f) + (style.FramePadding.Y * 2f),
            ImGui.GetFrameHeight()
        );
        var iconSize = new Vector2(nameBandHeight + style.ItemSpacing.Y + ImGui.GetFrameHeight());

        marketDataTableImageSize = iconSize;

        var origin      = ImGui.GetCursorPos();
        var iconTopLeft = ImGui.GetCursorScreenPos();

        // 图标用 DrawList 绘制以跨越两行；用透明按钮承接悬浮与点击
        ImGui.GetWindowDrawList().AddImage(itemIcon.Handle, iconTopLeft, iconTopLeft + iconSize);

        ImGui.SetCursorScreenPos(iconTopLeft);
        ImGui.InvisibleButton("###AutoBuyer-ItemIcon", iconSize);

        var isItemHovered = ImGui.IsItemHovered();
        var textX         = origin.X + iconSize.X + style.ItemSpacing.X;

        // 第一行：物品名（悬浮显示道具说明、左键复制名称）
        ImGui.SetCursorPos(new Vector2(textX, origin.Y + style.FramePadding.Y));

        using (UIFont(1.6f).Push())
        {
            ImGui.TextUnformatted
            (
                itemData.Name.ToString() +
                (frame.HQOnly ?
                     "\ue03c" :
                     string.Empty)
            );

            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                isItemHovered = true;
            }
        }

        // 持有数量 / 目标数量 / 购买按钮：与物品名同一行、贴右边缘。
        // 下移少许，使其在「物品名」所在行内垂直居中（视觉上像同一行）
        ImGui.SameLine();
        ImGui.SetCursorPosY(origin.Y + MathF.Max(0f, (nameBandHeight - ImGui.GetFrameHeight()) / 2f));
        DrawPurchaseControls(frame);

        // 第二行：收藏 / HQ / 刷新（与物品名左对齐，位于图标右侧）
        ImGui.SetCursorPos(new Vector2(textX, origin.Y + nameBandHeight + style.ItemSpacing.Y));

        using (ImRaii.Group())
        using (UIFont(0.8f).Push())
        {
            var isFavorite = config.FavoriteItems.ContainsKey(frame.ItemID);

            if (ImGui.Button
                (
                    isFavorite ?
                        "★" :
                        "☆"
                ))
            {
                if (isFavorite)
                    config.FavoriteItems.Remove(frame.ItemID);
                else
                {
                    config.FavoriteItems[frame.ItemID] = new MarketFavoriteItem
                    {
                        ItemID = frame.ItemID
                    };
                }

                favoriteItemsVersion++;
                SaveConfig(config);
            }

            ImGuiOm.TooltipHover(Lang.Get("Favorite"));

            if (itemData.CanBeHq)
            {
                ImGui.SameLine();

                using (ImRaii.PushColor(ImGuiCol.Text, KnownColor.GreenYellow.ToVector4(), frame.HQOnly))
                {
                    if (ImGui.Button("\ue03c###HQOnly"))
                        provider.ToggleHQ();
                }

                ImGuiOm.TooltipHover
                (
                    frame.HQOnly ?
                        $"{Lang.Get("All")}" :
                        $"{Lang.Get("BetterMarketBoard-MarketView-HQ")}"
                );
            }

            ImGui.SameLine();

            if (ImGui.Button(FontAwesomeIcon.Sync.ToIconString()))
                provider.Reload();

            ImGuiOm.TooltipHover(Lang.Get("BetterMarketBoard-ReloadMarketData"));

            // 「仅显示当前大区数据」勾选框与「展开全部世界价格」箭头：紧随刷新按钮之后（同行）
            ImGui.SameLine();

            DrawAllWorldPricesToggleComponent
            (
                isAllWorldsPriceExpanded ?
                    ImGuiDir.Up :
                    ImGuiDir.Down
            );
        }

        // 光标推进到图标下方，避免后续（价格卡片等）与图标/两行控件重叠
        ImGui.SetCursorPos(new Vector2(origin.X, origin.Y + iconSize.Y + style.ItemSpacing.Y));

        DrawAllWorldsPriceTable(frame);

        ImGui.Spacing();

        DrawMarketListings(frame, info);
    }

    /// <summary>
    /// 在售物品列表（原「市场数据」分页内容，已移除统计指标块与整单购买）。
    /// </summary>
    private void DrawMarketListings
    (
        MarketBoardUIContext frame,
        InfoProxyItemSearch* info
    )
    {
        if (InfoProxyItemSearch.IsListingsStuck)
        {
            // 请稍后再次确认。
            ImGui.TextColored(KnownColor.Orange.ToVector4(), $"（{LuminaWrapper.GetAddonText(1998)}）");
            return;
        }

        using (UIFont(0.8f).Push())
        {
            if (frame.IsViewingCurrentWorld)
            {
                // 本服：只显示游戏内实时数据。
                // 不再用 Universalis 兜底 —— 那些数据不能购买、价格不准，
                // 还会在购买/跨服期间短暂接管列表，看起来像「列表被刷新」。
                DrawLocalMarketDataTable(frame, info);
            }
            else if (provider.GetListingsDataSet(frame.ItemID) is { } onlineTwo)
                DrawOnlineMarketDataTable(frame, onlineTwo);
        }
    }

    private void DrawLocalMarketDataTable
    (
        MarketBoardUIContext frame,
        InfoProxyItemSearch* info
    )
    {
        var dataset       = provider.GetLocalListingsDataSet(info);
        var listingsArray = dataset.Listings;

        if (provider.IsImplicitRefreshPending || listingsArray.Count == 0)
            ImGui.TextDisabled($"{FontAwesomeIcon.Sync.ToIconString()} {Lang.Get("BetterMarketBoard-Purchase-Refreshing")}");

        var isAnyHQ              = dataset.IsAnyHQ;
        var isAnyMateriaEquipped = dataset.IsAnyMateria;

        var columnsCount = 4;
        if (isAnyHQ)
            columnsCount++;
        if (isAnyMateriaEquipped)
            columnsCount++;

        using var table = ImRaii.Table
            ("MarketBoardDataTable", columnsCount, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY, new(-1, ImGui.GetContentRegionAvail().Y));
        if (!table) return;

        ImGui.TableSetupScrollFreeze(0, 1);

        if (isAnyHQ)
            ImGui.TableSetupColumn("\ue03c", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("\ue03c").X);

        if (isAnyMateriaEquipped)
        {
            var materiaText = LuminaWrapper.GetAddonText(1937);
            ImGui.TableSetupColumn(materiaText, ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize(materiaText).X);
        }

        // 数据列四列平分（单价 / 数量 / 总价 / 雇员）
        ImGui.TableSetupColumn(LuminaWrapper.GetAddonText(357),  ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn(Lang.Get("Amount"),               ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn(LuminaWrapper.GetAddonText(6936), ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn(LuminaWrapper.GetAddonText(1956), ImGuiTableColumnFlags.WidthStretch, 1f);

        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

        if (isAnyHQ)
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted("\ue03c");
        }

        if (isAnyMateriaEquipped)
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(LuminaWrapper.GetAddonText(1937));
        }

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(LuminaWrapper.GetAddonText(357));

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Lang.Get("Amount"));

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(LuminaWrapper.GetAddonText(6936));

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(LuminaWrapper.GetAddonText(1956));

        var counter    = -1;
        var benchmarks = BuildMarketBenchmarks(frame.NPCGilPrice);

        foreach (var listing in listingsArray)
        {
            foreach (var b in benchmarks)
            {
                if (!b.Drawn && listing.UnitPrice > b.Price)
                {
                    DrawBenchmarkSeparatorRow($"LocalBenchmark_{b.Price}_{b.Badge}", b.Badge, b.Color, b.Tooltip, b.OnClick);
                    b.Drawn = true;
                }
            }

            counter++;

            using var id = ImRaii.PushId(listing.ListingId.ToString());

            var isOwnRetainer = IsOwnRetainer(listing.RetainerId);
            using var rowColor = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled), isOwnRetainer);

            // 行高固定为一行文字的 1.6 倍（原先行高取自表头按钮实测尺寸，明显偏高）
            ImGui.TableNextRow(ImGuiTableRowFlags.None, ImGui.GetTextLineHeight() * 1.6f);

            if (isAnyHQ)
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted
                (
                    listing.IsHqItem ?
                        "\u221a" :
                        string.Empty
                );
            }

            if (isAnyMateriaEquipped)
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{listing.MateriaCount}");
            }

            ImGui.TableNextColumn();
            DrawMarketPrice(listing.UnitPrice);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{listing.Quantity}");

            var totalPrice = listing.UnitPrice * listing.Quantity;

            ImGui.TableNextColumn();

            using (ImRaii.Disabled(isOwnRetainer))
                ImGui.Selectable($"{totalPrice.ToGilString()}\ue049", false, ImGuiSelectableFlags.SpanAllColumns);

            if (!isOwnRetainer)
            {
                using var popup = ImRaii.ContextPopupItem($"ExecuteBuyPopup_{listing.ListingId}");

                if (popup)
                {
                    ImGui.TextUnformatted($"{LuminaWrapper.GetAddonText(357)}:");

                    ImGui.SameLine();
                    ImGui.TextColored(KnownColor.LightSkyBlue.ToVector4(), $"{listing.UnitPrice.ToGilString()}\ue049");

                    ImGui.TextUnformatted($"{Lang.Get("Amount")}:");

                    ImGui.SameLine();
                    ImGui.TextColored(KnownColor.LightSkyBlue.ToVector4(), $"{listing.Quantity}");

                    ImGui.TextUnformatted($"{LuminaWrapper.GetAddonText(6936)}:");

                    ImGui.SameLine();
                    ImGui.TextColored(KnownColor.LightSkyBlue.ToVector4(), $"{totalPrice.ToGilString()}\ue049");

                    ImGui.Separator();
                    ImGui.Spacing();

                    if (ImGui.MenuItem(LuminaWrapper.GetAddonText(9275)))
                        MarketDataProvider.SendBuyRequest(listing);

                }
            }

            ImGui.TableNextColumn();

            // 雇员名来自游戏字符串数组，只能按行号取；已购行被本地隐藏后渲染行号会前移，
            // 必须用该挂单在游戏侧顺序中的原始行号，否则雇员名整列会错位（「只有雇员列在上移」）
            var sourceRowIndex = dataset.SourceRowIndexes.TryGetValue(listing.ListingId, out var rowIndex) ?
                                     rowIndex :
                                     counter;

            var retainerName = AtkStage.Instance()->GetStringArrayData(StringArrayType.ItemSearch)->StringArray[208 + (6 * sourceRowIndex)];
            if (retainerName.HasValue)
                ImGui.TextUnformatted($"{retainerName.ToString()}");
        }

        foreach (var b in benchmarks)
        {
            if (!b.Drawn)
            {
                DrawBenchmarkSeparatorRow($"LocalBenchmark_End_{b.Price}_{b.Badge}", b.Badge, b.Color, b.Tooltip, b.OnClick);
                b.Drawn = true;
            }
        }
    }

    private void DrawOnlineMarketDataTable
    (
        MarketBoardUIContext frame,
        ListingsDataSet      dataset
    )
    {
        var isAnyHQ = dataset.IsAnyHQ;

        // 列数 = 4 个数据列（单价 / 数量 / 总价 / 雇员） + 可选标记列。
        // 注意：必须用「+」而不是「-」——此前移植时沿用了原模块的减法写法，
        // 导致无 HQ 条目时列数算错，四列内容被折成 2×2（查看其他服务器时可见）。
        var columnsCount = 4;
        if (isAnyHQ)
            columnsCount++;

        using var table = ImRaii.Table
            ("UniversalisMarketDataTable", columnsCount, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY, new(-1, ImGui.GetContentRegionAvail().Y));
        if (!table) return;

        ImGui.TableSetupScrollFreeze(0, 1);

        if (isAnyHQ)
            ImGui.TableSetupColumn("\ue03c", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("\ue03c").X);

        // 数据列四列平分（单价 / 数量 / 总价 / 雇员）
        ImGui.TableSetupColumn(LuminaWrapper.GetAddonText(357),  ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn(Lang.Get("Amount"),               ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn(LuminaWrapper.GetAddonText(6936), ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn(LuminaWrapper.GetAddonText(1956), ImGuiTableColumnFlags.WidthStretch, 1f);

        ImGui.TableHeadersRow();

        var benchmarks = BuildMarketBenchmarks(frame.NPCGilPrice);

        foreach (var listing in dataset.Listings)
        {
            foreach (var b in benchmarks)
            {
                if (!b.Drawn && listing.PricePerUnit > b.Price)
                {
                    DrawBenchmarkSeparatorRow($"OnlineBenchmark_{b.Price}_{b.Badge}", b.Badge, b.Color, b.Tooltip, b.OnClick);
                    b.Drawn = true;
                }
            }

            using var id = ImRaii.PushId($"{listing.ListingID}");
            ImGui.TableNextRow();

            if (isAnyHQ)
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted
                (
                    listing.HQ ?
                        "√" :
                        string.Empty
                );
            }

            ImGui.TableNextColumn();
            DrawMarketPrice(listing.PricePerUnit);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{listing.Quantity}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{listing.Total.ToGilString()}\ue049");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{listing.RetainerName}");
        }

        foreach (var b in benchmarks)
        {
            if (!b.Drawn)
            {
                DrawBenchmarkSeparatorRow($"OnlineBenchmark_End_{b.Price}_{b.Badge}", b.Badge, b.Color, b.Tooltip, b.OnClick);
                b.Drawn = true;
            }
        }
    }
}
