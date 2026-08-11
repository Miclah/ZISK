using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using ZISK.Services;

namespace ZISK.Middleware;

/// <summary>
/// Locks a public ZISK_SEED_MODE=demo deployment down to a single visible way in: the /demo
/// landing page and its "Try as [Role]" buttons. Everything a recruiter could otherwise use to
/// reach the un-cloned template data directly - the password login form, registration, the
/// full Identity scaffold (password reset, email confirmation, 2FA management, ...) - 404s for
/// anyone without the owner cookie. A no-op entirely outside demo mode.
///
/// The owner (you) mints that cookie once via /__owner?key=&lt;Demo:OwnerKey&gt;, then /login
/// works normally and signs you into the template data (DemoSessionId == null) using the
/// demo passwords from azure/main.bicepparam - exactly the same accounts a fresh /demo clone
/// is copied from, without needing to go through a session cookie yourself.
/// </summary>
public class DemoAccessGuardMiddleware
{
    /// <summary>
    /// Also read by the application cookie's OnRedirectToLogin in Program.cs, which has to make the
    /// same owner-cookie decision this middleware does.
    /// </summary>
    public const string OwnerCookieName = "zisk_owner";

    // Case-insensitive path prefixes that must not be reachable by an anonymous recruiter.
    // "/Account" covers the whole ASP.NET Identity scaffold (password reset, 2FA, external
    // login, email confirmation, ...) mapped by MapAdditionalIdentityEndpoints() and the
    // Components/Account/Pages/* Razor pages, not just the two Slovak-routed pages.
    private static readonly string[] GuardedPathPrefixes =
    [
        "/login",
        "/registracia",
        "/overenie",
        "/Account"
    ];

    private readonly RequestDelegate _next;

    public DemoAccessGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// True when this visitor must be sent to /demo rather than /login, because demo mode is on
    /// and they hold no owner cookie - so <see cref="InvokeAsync"/> below would answer /login with
    /// a 404 and dead-end them.
    ///
    /// Every path that would otherwise hand someone to /login has to ask this: the application
    /// cookie's OnRedirectToLogin (Program.cs) for unauthenticated requests, and Logout.razor
    /// after signing out. Miss one and it 404s in demo mode while working fine everywhere else.
    /// </summary>
    public static bool ShouldRouteToDemoLanding(HttpRequest request, IConfiguration configuration) =>
        SeedModeResolver.Resolve(configuration) == SeedMode.Demo
        && !request.Cookies.ContainsKey(OwnerCookieName);

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        if (SeedModeResolver.Resolve(configuration) != SeedMode.Demo)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path;

        if (path.StartsWithSegments("/__owner", StringComparison.OrdinalIgnoreCase))
        {
            await GrantOwnerAccessAsync(context, configuration);
            return;
        }

        var isGuarded = GuardedPathPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
        if (isGuarded && !context.Request.Cookies.ContainsKey(OwnerCookieName))
        {
            await NotFoundResponseWriter.WriteAsync(context);
            return;
        }

        await _next(context);
    }

    private static async Task GrantOwnerAccessAsync(HttpContext context, IConfiguration configuration)
    {
        var expectedKey = configuration["Demo:OwnerKey"];
        var providedKey = QueryHelpers.ParseQuery(context.Request.QueryString.Value ?? "")
            .TryGetValue("key", out var values) ? values.ToString() : null;

        if (string.IsNullOrEmpty(expectedKey) || string.IsNullOrEmpty(providedKey)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedKey),
                Encoding.UTF8.GetBytes(providedKey)))
        {
            await NotFoundResponseWriter.WriteAsync(context);
            return;
        }

        context.Response.Cookies.Append(OwnerCookieName, "1", new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });

        context.Response.Redirect("/login");
    }
}
