namespace ZISK.Client.Services;

public sealed class HttpActivityTracker
{
    private int _activeCount;

    public event Action? StateChanged;

    public bool IsActive => Volatile.Read(ref _activeCount) > 0;

    public int ActiveCount => Volatile.Read(ref _activeCount);

    public void Begin()
    {
        Interlocked.Increment(ref _activeCount);
        StateChanged?.Invoke();
    }

    public void End()
    {
        var remaining = Interlocked.Decrement(ref _activeCount);
        if (remaining < 0)
            Interlocked.Exchange(ref _activeCount, 0);
        StateChanged?.Invoke();
    }
}
