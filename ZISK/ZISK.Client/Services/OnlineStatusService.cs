using Microsoft.JSInterop;

namespace ZISK.Client.Services;

public class OnlineStatusService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<OnlineStatusService>? _dotNetRef;

    public event Action? StatusChanged;
    public bool IsOnline { get; private set; } = true;

    public OnlineStatusService(IJSRuntime js) => _js = js;

    public async Task InitializeAsync()
    {
        _dotNetRef = DotNetObjectReference.Create(this);
        IsOnline = await _js.InvokeAsync<bool>("onlineStatus.initialize", _dotNetRef);
    }

    [JSInvokable]
    public void UpdateOnlineStatus(bool isOnline)
    {
        IsOnline = isOnline;
        StatusChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        try { await _js.InvokeVoidAsync("onlineStatus.dispose"); } catch { }
        _dotNetRef?.Dispose();
    }
}
