using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using ZISK.Client.Services;
using ZISK.Shared.Localization;

namespace ZISK.Client.Components;

/// <summary>
/// Drop-in replacement for the built-in &lt;DataAnnotationsValidator /&gt;. ZISK.Shared DTOs now
/// carry a Translations key in their DataAnnotations ErrorMessage (e.g.
/// "validation.name.required") instead of literal Slovak text - see Krok 6C in
/// docs/DEMO_MODE_HANDOFF.md - because the same attribute is evaluated both here (in-browser)
/// and again server-side via ApiBehaviorOptions.InvalidModelStateResponseFactory. This component
/// runs the identical validation logic as the built-in one (whole-object on submit, single-field
/// on each edit) but resolves that key through the current UI language before it reaches the
/// ValidationMessageStore.
/// </summary>
public class LocalizedDataAnnotationsValidator : ComponentBase, IDisposable
{
    [CascadingParameter] private EditContext? CurrentEditContext { get; set; }
    [Inject] private ILanguageService LanguageService { get; set; } = default!;

    private ValidationMessageStore? _messageStore;

    protected override void OnInitialized()
    {
        if (CurrentEditContext is null)
        {
            throw new InvalidOperationException(
                $"{nameof(LocalizedDataAnnotationsValidator)} requires a cascading parameter of type " +
                $"{nameof(EditContext)}. For example, use it inside an EditForm.");
        }

        _messageStore = new ValidationMessageStore(CurrentEditContext);
        CurrentEditContext.OnValidationRequested += HandleValidationRequested;
        CurrentEditContext.OnFieldChanged += HandleFieldChanged;
        LanguageService.Changed += HandleLanguageChanged;
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        var editContext = (EditContext)sender!;
        _messageStore!.Clear();

        var validationContext = new ValidationContext(editContext.Model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(editContext.Model, validationContext, results, validateAllProperties: true);

        foreach (var result in results)
        {
            var message = Translations.Get(LanguageService.Current, result.ErrorMessage ?? string.Empty);
            var hasMember = false;
            foreach (var memberName in result.MemberNames)
            {
                hasMember = true;
                _messageStore.Add(editContext.Field(memberName), message);
            }
            if (!hasMember)
            {
                _messageStore.Add(new FieldIdentifier(editContext.Model, string.Empty), message);
            }
        }

        editContext.NotifyValidationStateChanged();
    }

    private void HandleFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        var editContext = CurrentEditContext!;
        var fieldIdentifier = e.FieldIdentifier;
        var propertyInfo = fieldIdentifier.Model.GetType().GetProperty(fieldIdentifier.FieldName,
            BindingFlags.Public | BindingFlags.Instance);

        if (propertyInfo is null || !propertyInfo.CanRead)
            return;

        var propertyValue = propertyInfo.GetValue(fieldIdentifier.Model);
        var validationContext = new ValidationContext(fieldIdentifier.Model) { MemberName = propertyInfo.Name };
        var results = new List<ValidationResult>();
        Validator.TryValidateProperty(propertyValue, validationContext, results);

        _messageStore!.Clear(fieldIdentifier);
        _messageStore.Add(fieldIdentifier,
            results.Select(r => Translations.Get(LanguageService.Current, r.ErrorMessage ?? string.Empty)));

        editContext.NotifyValidationStateChanged();
    }

    private void HandleLanguageChanged()
    {
        // Only re-run validation (which re-populates messages in the new language) if there's
        // already something invalid to re-translate - a pristine, untouched form shouldn't
        // suddenly grow "required" errors just because the visitor flipped the flag.
        if (CurrentEditContext is { } editContext && editContext.GetValidationMessages().Any())
            editContext.Validate();
    }

    public void Dispose()
    {
        LanguageService.Changed -= HandleLanguageChanged;
        if (CurrentEditContext is not null)
        {
            CurrentEditContext.OnValidationRequested -= HandleValidationRequested;
            CurrentEditContext.OnFieldChanged -= HandleFieldChanged;
        }
    }
}
