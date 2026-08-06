namespace ZISK.Services;

/// <summary>
/// Coarse phase of the one-time database initialization that runs at startup.
///
/// These are real observed states, deliberately not a percentage. Neither App Service nor Azure SQL
/// serverless exposes any progress signal for a container start or a database resume, so a number
/// here could only ever be a timer dressed up as progress - which would be a worse answer than an
/// honest "waking the database up".
/// </summary>
public enum StartupPhase
{
    /// <summary>Waiting for the first successful connection. On the demo this is the auto-paused Azure SQL resume.</summary>
    WakingDatabase,
    Migrating,
    Seeding,
    Ready,
    Failed
}

/// <summary>
/// Shared, singleton view of how far <see cref="DatabaseInitializationService"/> has got. Read by
/// StartupGateMiddleware to decide whether a request may reach the app, and awaited by the hosted
/// workers before their first pass.
/// </summary>
public sealed class StartupState
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _phase = (int)StartupPhase.WakingDatabase;
    private int _attempt;
    private volatile string? _error;

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    public StartupPhase Phase => (StartupPhase)Volatile.Read(ref _phase);
    public int Attempt => Volatile.Read(ref _attempt);
    public string? Error => _error;
    public bool IsReady => Phase == StartupPhase.Ready;

    public TimeSpan Elapsed => DateTimeOffset.UtcNow - StartedAt;

    public void EnterPhase(StartupPhase phase) => Volatile.Write(ref _phase, (int)phase);

    public void RecordAttempt(int attempt) => Volatile.Write(ref _attempt, attempt);

    public void MarkReady()
    {
        _error = null;
        Volatile.Write(ref _phase, (int)StartupPhase.Ready);
        _ready.TrySetResult();
    }

    public void MarkFailed(string error)
    {
        _error = error;
        Volatile.Write(ref _phase, (int)StartupPhase.Failed);
    }

    /// <summary>
    /// Completes once initialization has succeeded.
    ///
    /// The hosted workers await this before their first pass. With initialization moved off the
    /// startup path they now start at the same time as it rather than after it, and three of them
    /// (attendance auto-close, demo template refresh, demo session cleanup) query the database in
    /// their very first iteration - without this gate they would run against a database that has no
    /// schema yet. It intentionally never completes while initialization keeps failing: a worker
    /// that cannot reach a ready database has nothing useful to do.
    /// </summary>
    public Task WaitForReadyAsync(CancellationToken ct) => _ready.Task.WaitAsync(ct);
}
