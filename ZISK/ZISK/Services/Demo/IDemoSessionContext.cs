namespace ZISK.Services.Demo;

/// <summary>
/// Resolves which demo session (if any) the current request belongs to. Backed primarily by
/// the <see cref="DemoSessionContext.ClaimType"/> claim on the signed-in user - stamped once
/// at "Try as [Role]" sign-in - with a fallback to the <see cref="DemoSessionContext.CookieName"/>
/// cookie for anonymous requests (e.g. the demo landing page itself, before sign-in). The claim
/// is primary because it cannot drift from who is actually logged in: if a visitor clears their
/// cookie mid-session, their auth cookie still identifies which demo session they belong to.
/// Always null outside ZISK_SEED_MODE=demo, which is what makes
/// <see cref="ZISK.Data.ApplicationDbContext"/>'s per-entity query filter a complete no-op in
/// Local/Production mode.
/// </summary>
public interface IDemoSessionContext
{
    Guid? Current { get; }
}
