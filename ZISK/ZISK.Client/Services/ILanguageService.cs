using ZISK.Shared.Localization;

namespace ZISK.Client.Services;

/// <summary>
/// Client-side (WASM) language switch. <see cref="Changed"/> fires on every switch so any
/// component that wants to re-render in the new language just needs to subscribe - see
/// <see cref="LocalizedComponentBase"/>, which every translated page/component inherits from
/// instead of wiring this up by hand.
/// </summary>
public interface ILanguageService
{
    Lang Current { get; }
    event Action? Changed;

    /// <summary>Reads the persisted choice (zisk_lang cookie) once at app startup.</summary>
    Task InitializeAsync();

    Task SetAsync(Lang lang);

    /// <summary>Shorthand for <c>Translations.Get(Current, key)</c>.</summary>
    string this[string key] { get; }
}
