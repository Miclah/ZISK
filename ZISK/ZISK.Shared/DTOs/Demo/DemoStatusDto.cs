namespace ZISK.Shared.DTOs.Demo;

/// <summary>
/// Whether the app is running in public-demo mode, and (if the caller already has a demo
/// session) when that session's data is due to be cleaned up. Anonymous callers can hit this -
/// it's how the WASM client decides whether an unauthenticated visitor should be sent to
/// /demo instead of /login (see RedirectToLogin.razor).
/// </summary>
public record DemoStatusDto(bool IsDemo, DateTime? SessionExpiresAt);
