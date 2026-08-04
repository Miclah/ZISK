using Microsoft.AspNetCore.Components;
using ZISK.Client.Services;

namespace ZISK.Client.Components;

/// <summary>
/// Base class for any translated page/component: <c>@inherits LocalizedComponentBase</c>, then
/// use <c>@T("some.key")</c> instead of hardcoded Slovak text. Subscribes to
/// <see cref="ILanguageService.Changed"/> so a language switch re-renders this component
/// immediately - no page reload, which is the whole point of driving this off a client-side
/// dictionary instead of .resx satellite assemblies (see CLAUDE.md's localization notes).
/// </summary>
public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    [Inject] protected ILanguageService LanguageService { get; set; } = default!;

    protected override void OnInitialized()
    {
        LanguageService.Changed += OnLanguageChanged;
        base.OnInitialized();
    }

    /// <summary>Looks up <paramref name="key"/> in the current language.</summary>
    protected string T(string key) => LanguageService[key];

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        LanguageService.Changed -= OnLanguageChanged;
    }
}
