using Lumina.Text.ReadOnly;
using OmenTools.OmenService;

namespace FFXIVAutoBuyer.Manager;

/// <summary>
/// 本地化文案入口（与 DailyRoutines 的 <c>Lang</c> 别名行为一致）。
/// </summary>
public static class LanguageManager
{
    public static string Get
    (
        string key
    ) =>
        LocalizationManager.Instance().Get(key);

    public static string Get
    (
        string   key,
        params object[] args
    ) =>
        LocalizationManager.Instance().Get(key, args);

    public static ReadOnlySeString GetSe
    (
        string   key,
        params object[] args
    ) =>
        LocalizationManager.Instance().GetSe(key, args);
}
