using DailyRoutines.Common.Module.Abstractions;
using OmenTools.Utils.FuzzyMatcher;

namespace DailyRoutines.Common.Module;

public sealed class ModuleSearcher
{
    private static readonly FuzzySearchWeight TitleWeight = new(400, 24, 18, 16, 200, 3);
    private static readonly FuzzySearchWeight NameWeight  = new(320, 20, 14, 12, 160, 4);
    private static readonly FuzzySearchWeight MetaWeight  = new(200, 14, 10, 8, 100, 5);

    private readonly FuzzyMatcher<ModuleBase> matcher;

    public ModuleSearcher
    (
        IEnumerable<ModuleBase>                  modules,
        Func<ModuleBase, IEnumerable<string?>?>? extraSegmentsSelector = null
    )
    {
        ArgumentNullException.ThrowIfNull(modules);

        matcher = new FuzzyMatcher<ModuleBase>
        (
            modules.Where(static m => m != null),
            module =>
            {
                var info       = module.Info;
                var moduleType = module.GetType();
                var extra      = extraSegmentsSelector?.Invoke(module);

                return
                [
                    ([info.Title], TitleWeight),
                    ([module.ModuleName, moduleType.Name], NameWeight),
                    (
                        [
                            string.Join(" / ", info.Author),
                            info.Description,
                            string.Join(" / ", info.ModulesPrerequisite),
                            string.Join(" / ", info.ModulesRecommend),
                            string.Join(" / ", info.ModulesConflict),
                            string.Join(" / ", module.PrecedingModules?.Select(static x => x.ModuleName) ?? []),
                            string.Join(" / ", module.RecommendModules?.Select(static x => x.ModuleName) ?? []),
                            string.Join(" / ", module.ConflictModules?.Select(static x => x.ModuleName)  ?? []),
                            info.ReportURL,
                            string.Join(" / ", info.SupportUrls),
                            string.Join(" / ", info.PreviewImageURL),
                            .. extra ?? []
                        ],
                        MetaWeight
                    )
                ];
            }
        );
    }

    public ModuleBase[] Search
    (
        string query
    ) =>
        matcher.Search
        (
            query,
            static (left, right) =>
                string.Compare
                (
                    left.Info.Title,
                    right.Info.Title,
                    StringComparison.OrdinalIgnoreCase
                )
        );
}
