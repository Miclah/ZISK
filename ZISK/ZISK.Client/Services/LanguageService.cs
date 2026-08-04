using System.Globalization;
using Microsoft.JSInterop;
using ZISK.Client.Display;
using ZISK.Shared.Localization;

namespace ZISK.Client.Services;

public class LanguageService : ILanguageService
{
    private readonly IJSRuntime _js;

    public Lang Current { get; private set; } = Lang.Sk;
    public event Action? Changed;

    public LanguageService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var cookie = await _js.InvokeAsync<string?>("ziskLang.getCookie");
            Current = cookie == "en" ? Lang.En : Lang.Sk;
        }
        catch
        {
            // JS interop unavailable (shouldn't happen post-WASM-boot, but never let a missing
            // cookie helper crash startup) - default to Slovak.
            Current = Lang.Sk;
        }

        DomainLabels.Current = Current;
        ApplyCulture(Current);
    }

    public async Task SetAsync(Lang lang)
    {
        if (Current == lang)
            return;

        Current = lang;
        DomainLabels.Current = lang;
        ApplyCulture(lang);

        try
        {
            await _js.InvokeVoidAsync("ziskLang.setCookie", lang == Lang.En ? "en" : "sk");
        }
        catch { }

        Changed?.Invoke();
    }

    public string this[string key] => Translations.Get(Current, key);

    /// <summary>
    /// Switches the thread culture along with the language.
    ///
    /// Translations.Get only covers text this app writes itself. Anything the framework or a
    /// third-party component formats - month and weekday names, date and number formats - reads
    /// CultureInfo.CurrentCulture instead, so leaving it pinned to sk-SK left the training
    /// calendar (Heron.MudCalendar) showing "august 2026" and "po ut st" while the rest of the UI
    /// was English.
    ///
    /// en-GB rather than en-US on purpose: it starts the week on Monday and uses day-first dates,
    /// so the calendar grid and every date in the app keep the same shape in both languages.
    /// Both shards ship in the pinned icudt_no_CJK.dat (see the globalization note in CLAUDE.md).
    /// </summary>
    private static void ApplyCulture(Lang lang)
    {
        var culture = new CultureInfo(lang == Lang.En ? "en-GB" : "sk-SK");

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
