namespace ZISK.Services.Demo;

/// <summary>Bound from the "Demo" configuration section. Only consulted when ZISK_SEED_MODE=demo.</summary>
public class DemoOptions
{
    /// <summary>
    /// Shared secret for the one-time /__owner?key=... link that grants the owner-only cookie
    /// (see DemoAccessGuardMiddleware). Not required outside demo mode.
    /// </summary>
    public string? OwnerKey { get; set; }

    /// <summary>
    /// Free-tier safety valve: when the number of live demo sessions would exceed this, the
    /// least-recently-active one is deleted before a new one is cloned.
    /// </summary>
    public int MaxActiveSessions { get; set; } = 200;
}
