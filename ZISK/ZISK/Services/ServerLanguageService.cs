using ZISK.Client.Services;
using ZISK.Shared.Localization;

namespace ZISK.Services;

/// <summary>
/// Server-side implementation of the client's <see cref="ILanguageService"/>.
///
/// It has to exist because MainLayout injects ILanguageService, and MainLayout is rendered
/// *on the server* on every first request: App.razor hands out a null render mode until the
/// browser asks for interactive routing, so the whole component tree goes through static SSR
/// first. Without this registration that render throws "There is no registered service of type
/// ZISK.Client.Services.ILanguageService" - which surfaces as a 500 rather than a page.
///
/// The client's LanguageService cannot be reused here: it reads the zisk_lang cookie through
/// IJSRuntime, and there is no JS interop during static SSR. This one reads the same cookie
/// straight off the request instead, so a visitor who picked EN on /demo keeps EN through the
/// server-rendered pass too.
///
/// Deliberately Scoped, not Singleton like the WASM registration - on the server one instance
/// is shared by every user, so a per-process language would leak one visitor's choice to
/// everyone. For the same reason this does not touch the static DomainLabels.Current the client
/// sets; that is per-browser state on the client and would be cross-request state here.
/// </summary>
public class ServerLanguageService : ILanguageService
{
    public Lang Current { get; private set; }

    /// <summary>Never fires server-side: a static SSR pass renders once and is done.</summary>
    public event Action? Changed;

    public ServerLanguageService(IHttpContextAccessor accessor)
    {
        var cookie = accessor.HttpContext?.Request.Cookies["zisk_lang"];
        Current = cookie == "en" ? Lang.En : Lang.Sk;
    }

    /// <summary>No-op: the language is already resolved from the cookie in the constructor.</summary>
    public Task InitializeAsync() => Task.CompletedTask;

    public Task SetAsync(Lang lang)
    {
        // Nothing to persist here - the cookie is written by whoever owns the actual switch
        // (DemoLanding server-side, LanguageService via JS on the client).
        Current = lang;
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public string this[string key] => Translations.Get(Current, key);
}
