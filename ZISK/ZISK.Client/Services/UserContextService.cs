namespace ZISK.Client.Services;

public record UserContextOption(string Key, string Label, string? ChildId, Guid? TeamId, bool IsOwnProfile);

public class UserContextService
{
    public const string AllKey = "all"; // sentinel value meaning "show all children at once" — this is not a real user ID

    private List<UserContextOption> _options = [];

    public IReadOnlyList<UserContextOption> Options => _options;
    public string SelectedKey { get; private set; } = AllKey;

    // Blazor components subscribe manually and call StateHasChanged() on this event — changes do not propagate automatically like INotifyPropertyChanged.
    public event Action? Changed;

    public UserContextOption? SelectedOption => _options.FirstOrDefault(o => o.Key == SelectedKey);

    public void SetOptions(IEnumerable<UserContextOption> options)
    {
        _options = options.ToList();

        if (!_options.Any())
        {
            SelectedKey = AllKey;
            Changed?.Invoke();
            return;
        }

        // If the currently selected key no longer exists in the new option list, reset to the first available option.
        // This prevents a stale "selected child" state after the parent's children list is refreshed.
        if (!_options.Any(o => o.Key == SelectedKey))
        {
            SelectedKey = _options[0].Key;
            Changed?.Invoke();
        }
    }

    public void SetSelected(string key)
    {
        if (SelectedKey == key)
            return;

        if (!_options.Any(o => o.Key == key))
            return;

        SelectedKey = key;
        Changed?.Invoke();
    }
}
