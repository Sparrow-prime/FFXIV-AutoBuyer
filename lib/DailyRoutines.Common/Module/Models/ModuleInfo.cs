using DailyRoutines.Common.Module.Enums;
using Newtonsoft.Json;
using OmenTools.OmenService;

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

    public string[] SupportUrls => Author
                                   .Select(x => AuthorSupportLinks.TryGetValue(x, out var link)
                                                    ? link.SupportLink
                                                    : string.Empty)
                                   .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

    public override string ToString() => Title;

    private const string AUTHOR_SUPPORT_LINKS_BASE_URL =
        "https://gh.atmoomen.top/raw.githubusercontent.com/Dalamud-DailyRoutines/DailyRoutines.Info/master/assets/AuthorSupportLinks.json";
    
    public static Dictionary<string, LinkInfo> AuthorSupportLinks { get; private set; } = [];

    static ModuleInfo()
    {
        using var cancelSource = HTTPClientHelper.Instance().AcquireSharedCancellation();
        var token = cancelSource.Token;
        Task.Run
        (
            async () =>
            {
                try
                {
                    var text = await HTTPClientHelper.Instance().Get().GetStringAsync(AUTHOR_SUPPORT_LINKS_BASE_URL, token);
                    AuthorSupportLinks = JsonConvert.DeserializeObject<Dictionary<string, LinkInfo>>(text) ?? new();
                }
                catch
                {
                    // ignored
                }
            },
            token
        );
    }
    
    public class LinkInfo
    {
        public string GitHubLink  { get; set; } = string.Empty;
        public string SupportLink { get; set; } = string.Empty;

        public string IconLink
        {
            get
            {
                if (!string.IsNullOrEmpty(field)) return field;
                if (string.IsNullOrWhiteSpace(GitHubLink)) return string.Empty;
                
                field = GitHubLink.Replace("https://github.com", "https://gh.atmoomen.top/avatars.githubusercontent.com");
                return field;
            }
        }
    }
}
