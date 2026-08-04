using System.ComponentModel.DataAnnotations;

namespace ZISK.Data.Entities;

/// <summary>
/// One row per demo visitor (identified by the <c>zisk_demo_session</c> cookie / the
/// <c>zisk:demo-session</c> claim stamped on their cloned users at "Try as" sign-in).
/// Only meaningful when ZISK_SEED_MODE=demo. <see cref="Id"/> is also the value stored as
/// <see cref="IDemoScoped.DemoSessionId"/> on every row cloned for this visitor.
/// </summary>
public class DemoSession
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Bumped on every authenticated request tied to this session (see
    /// DemoSessionActivityMiddleware). DemoSessionCleanupWorker deletes sessions whose
    /// LastSeenAt is more than 24h old, independent of the 30-day cookie lifetime - the
    /// cookie only lets a returning visitor find their session *if it still exists*.
    /// </summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string? CreatedFromRole { get; set; }
}

/// <summary>
/// Single-row table (Id is always 1) tracking when the shared demo template data
/// (DemoSessionId == null rows) was last (re)generated. DemoTemplateRefreshWorker uses this
/// to decide whether the template is stale and needs regenerating so seeded trainings/
/// announcements stay anchored close to "now" indefinitely, not just at first deploy.
/// </summary>
public class DemoTemplateMeta
{
    public int Id { get; set; } = 1;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
