namespace ZISK.Client.Services;

public record UserContextOption(string Key, string Label, Guid? ChildId, Guid? TeamId, bool IsOwnProfile);

public class UserContextService
{
    public const string AllKey = "all";

    private List<UserContextOption> _options = [];

    public IReadOnlyList<UserContextOption> Options => _options;
    public string SelectedKey { get; private set; } = AllKey;

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
