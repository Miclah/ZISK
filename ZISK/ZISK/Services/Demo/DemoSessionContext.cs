namespace ZISK.Services.Demo;

public class DemoSessionContext : IDemoSessionContext
{
    /// <summary>Claim stamped on a demo user at "Try as [Role]" sign-in. Primary source.</summary>
    public const string ClaimType = "zisk:demo-session";

    /// <summary>
    /// 30-day persistent cookie set on first visit to /demo. Lets a returning visitor (same
    /// browser, no active sign-in yet) be routed back to their own session's clone instead of
    /// getting a brand new one. Fallback source only - see <see cref="IDemoSessionContext"/>.
    /// </summary>
    public const string CookieName = "zisk_demo_session";

    private readonly Guid? _current;

    public DemoSessionContext(IHttpContextAccessor accessor, IConfiguration configuration)
    {
        _current = Resolve(accessor.HttpContext, configuration);
    }

    public Guid? Current => _current;

    private static Guid? Resolve(HttpContext? http, IConfiguration configuration)
    {
        if (http is null || SeedModeResolver.Resolve(configuration) != SeedMode.Demo)
            return null;

        var claimValue = http.User?.FindFirst(ClaimType)?.Value;
        if (Guid.TryParse(claimValue, out var claimSessionId))
            return claimSessionId;

        if (http.Request.Cookies.TryGetValue(CookieName, out var cookieValue)
            && Guid.TryParse(cookieValue, out var cookieSessionId))
            return cookieSessionId;

        return null;
    }
}
