namespace ZISK.Data.Entities;

/// <summary>
/// Marks an entity as demo-session-scoped. When ZISK_SEED_MODE=demo, every row carries a
/// <see cref="DemoSessionId"/>: null for the shared template data, or a specific session's
/// id for that visitor's private clone. <see cref="ApplicationDbContext"/> applies a global
/// query filter over every <see cref="IDemoScoped"/> entity so no service or controller code
/// has to know about demo sessions at all - the same pattern as <c>TeamAccessService</c>, just
/// enforced one layer lower. Outside demo mode the filter is a no-op (see
/// <c>IDemoSessionContext</c>).
/// </summary>
public interface IDemoScoped
{
    Guid? DemoSessionId { get; set; }
}
