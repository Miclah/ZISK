using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ZISK.Services;
using ZISK.Services.Demo;
using ZISK.Shared.DTOs.Demo;
using ZISK.Shared.Localization;

namespace ZISK.Controllers;

/// <summary>
/// Deliberately does not carry the app's usual blanket [Authorize] - GetStatus must be
/// reachable by anonymous visitors (RedirectToLogin.razor calls it before the visitor is
/// signed in, to decide between /demo and /login), so [Authorize] is applied per-action here
/// instead of at the controller level.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DemoController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IDemoSessionContext _demoSessionContext;
    private readonly IDemoSessionService _demoSessionService;
    private readonly ICurrentLanguage _currentLanguage;

    public DemoController(
        IConfiguration configuration,
        IDemoSessionContext demoSessionContext,
        IDemoSessionService demoSessionService,
        ICurrentLanguage currentLanguage)
    {
        _configuration = configuration;
        _demoSessionContext = demoSessionContext;
        _demoSessionService = demoSessionService;
        _currentLanguage = currentLanguage;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public ActionResult<DemoStatusDto> GetStatus()
    {
        var isDemo = SeedModeResolver.Resolve(_configuration) == SeedMode.Demo;
        return Ok(new DemoStatusDto(isDemo, null));
    }

    [HttpPost("reset")]
    [Authorize]
    public async Task<IActionResult> Reset()
    {
        if (SeedModeResolver.Resolve(_configuration) != SeedMode.Demo)
            return NotFound();

        var sessionId = _demoSessionContext.Current;
        if (sessionId == null)
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.demo.notInSession"));

        var newSessionId = await _demoSessionService.ResetSessionAsync(sessionId.Value);

        // Point the visitor's cookie at the freshly-cloned session. Without this, the next
        // "Try as [Role]" click on /demo would read the now-deleted old session id from the
        // stale cookie, find it gone, and clone a *third* time - wasteful, and it would orphan
        // the session ResetSessionAsync just created until the cleanup worker sweeps it.
        Response.Cookies.Append(DemoSessionContext.CookieName, newSessionId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });

        // The signed-in user's own account row was just deleted as part of the reset (a fresh
        // clone was made under a brand new session id), so the current auth cookie now points
        // at a nonexistent user. Sign out here rather than try to sign the caller back in to
        // the new clone - the client redirects to /demo, where "Try as [Role]" starts them
        // fresh in the new session via its own cookie.
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        return Ok();
    }
}
