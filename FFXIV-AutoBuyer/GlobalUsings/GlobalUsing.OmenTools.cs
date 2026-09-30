// ReSharper disable RedundantUsingDirective.Global

#region OmenTools

global using OmenTools;
global using OmenTools.ImGuiOm;
global using OmenTools.Extensions;
global using OmenTools.Info.Game.Enums;
global using OmenTools.OmenService;
// 【第五十六轮】删除了 16 条 `global using IX = OmenTools.Dalamud.Services...` 别名：
// 它们全部只出现在这一行、插件代码里没有任何使用点，却让「OmenTools 抽象层是否被引用」的
// 静态扫描全部误判为「被引用」（别名本身也算一次类型提及）。实际用到的只有
// `DService.Instance().ObjectTable`（其返回类型由 DService 属性决定，不需要别名）。
global using static OmenTools.Global.Globals;
global using static OmenTools.Info.Game.Data.Addons;

#endregion

#region Dalamud

global using Dalamud.Bindings.ImGui;
global using Dalamud.Bindings.ImGuizmo;
global using Dalamud.Bindings.ImPlot;
global using Dalamud.Interface;
global using Dalamud.Interface.Utility.Raii;
global using Dalamud.Game;
global using Dalamud.Plugin;
global using Dalamud.Plugin.Services;

#endregion

#region C#

global using System.Drawing;

#endregion
