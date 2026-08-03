using ZISK.Data;

namespace ZISK.Services.Demo;

/// <summary>
/// Owns the lifecycle of a demo visitor's private clone of the shared template data: create
/// (by cloning every DemoSessionId == null row into a brand new session id), look up which
/// cloned user plays a given role for "Try as [Role]" sign-in, wipe a session's data, and
/// reset (wipe + reclone). Every method here deliberately bypasses the ambient
/// ApplicationDbContext query filter (via IgnoreQueryFilters) because this service is the one
/// place in the app that is allowed to see - and move data between - multiple sessions at
/// once; everywhere else the filter is what keeps sessions apart.
/// </summary>
public interface IDemoSessionService
{
    /// <summary>
    /// Returns <paramref name="cookieSessionId"/> unchanged if it still exists (touching its
    /// LastSeenAt), otherwise clones the template into a brand new session and returns its id.
    /// </summary>
    Task<Guid> EnsureSessionAsync(Guid? cookieSessionId, string? createdFromRole = null);

    /// <summary>
    /// Finds the cloned user that plays <paramref name="role"/> ("Admin", "Coach", "Parent",
    /// or "Child") within the given session, for "Try as [Role]" sign-in. Null if the session
    /// or that role's template account doesn't exist.
    /// </summary>
    Task<ApplicationUser?> GetUserForRoleAsync(Guid sessionId, string role);

    /// <summary>Deletes every row (and any files) belonging to a session. Optionally the session row itself.</summary>
    Task DeleteSessionDataAsync(Guid sessionId, bool alsoDeleteSessionRow);

    /// <summary>Wipes a session's data and immediately re-clones a fresh copy under a new session id.</summary>
    Task<Guid> ResetSessionAsync(Guid sessionId);

    Task TouchAsync(Guid sessionId);
}
