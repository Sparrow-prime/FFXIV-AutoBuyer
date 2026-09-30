using DailyRoutines.Common.Module.Enums;

namespace DailyRoutines.Common.Module.Models;

public sealed class ModuleInfo
{
    public required string         Title               { get; init; }
    public required string         Description         { get; init; }
    public required ModuleCategory Category            { get; init; }
    public          string[]       Author              { get; init; } = ["AtmoOmen"];
    public          string         ReportURL           { get; init; } = "https://discord.com/channels/1258981591124938762/1464230937653940316";
    public          string[]       ModulesPrerequisite { get; init; } = [];
    public          string[]       ModulesRecommend    { get; init; } = [];
    public          string[]       ModulesConflict     { get; init; } = [];
    public          string[]       PreviewImageURL     { get; init; } = [];

    // 【AutoBuyer 本地改动】删除「作者支持链接」：
    // 原实现在**静态构造**里 Task.Run 请求 gh.atmoomen.top 拉取 AuthorSupportLinks.json，
    // 即插件一加载就外发一次网络请求；而唯一的消费者 SupportUrls 只被
    // ModuleSearcher.cs 使用，该文件已被本工程裁剪（见 DailyRoutines.Common.csproj），
    // 因此整块（静态构造 + SupportUrls + LinkInfo）在本插件中完全无效，已整体移除。

    public override string ToString() => Title;
}
