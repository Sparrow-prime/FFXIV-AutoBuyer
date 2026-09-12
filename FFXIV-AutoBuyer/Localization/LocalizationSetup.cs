using System.Collections.Frozen;
using Lumina.Data;
using OmenTools.Localization;
using OmenTools.Localization.Parsers;
using OmenTools.OmenService;

namespace FFXIVAutoBuyer.Localization;

/// <summary>
/// 接入 OmenTools 的 <see cref="LocalizationManager"/>，加载随插件发布的词条文件。
/// </summary>
internal static class LocalizationSetup
{
    private const string DefaultFileName = "zh-CN.json";

    public static void Configure
    (
        IDalamudPluginInterface pluginInterface
    )
    {
        var directory = Path.Join(pluginInterface.AssemblyLocation.Directory?.FullName, "Localization");

        LocalizationManager.Instance().Configure
        (
            new LocalizationOptions
            {
                SupportedLanguages = new Dictionary<Language, string>
                {
                    [Language.ChineseSimplified] = "简体中文",
                    [Language.English]           = "English"
                }.ToFrozenDictionary(),
                DefaultLanguage = Language.ChineseSimplified,
                FileNameResolver = ResolveFileName,
                Source           = new FileLocalizationSource(directory),
                Parser           = new JsonDictionaryLocalizationParser(),
                FallbackResolver = ResolveFallback,
                EnableHotReload  = true
            },
            Language.ChineseSimplified,
            ResolvePreferredLanguage(pluginInterface.UiLanguage)
        );
    }

    private static string ResolveFileName
    (
        Language language
    ) =>
        language switch
        {
            Language.English => "en-US.json",
            _                => DefaultFileName
        };

    private static IEnumerable<Language> ResolveFallback
    (
        Language language
    ) =>
        language == Language.ChineseSimplified ?
            [] :
            [Language.ChineseSimplified];

    private static Language ResolvePreferredLanguage
    (
        string uiLanguage
    ) =>
        uiLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase) ?
            Language.English :
            Language.ChineseSimplified;
}
