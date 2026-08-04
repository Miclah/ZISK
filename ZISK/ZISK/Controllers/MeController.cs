using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZISK.Data;
using ZISK.Services;
using ZISK.Services.Demo;
using ZISK.Shared.DTOs.Users;
using ZISK.Shared.Localization;

namespace ZISK.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditService _auditService;
    private readonly IUserService _userService;
    private readonly ICurrentLanguage _currentLanguage;
    private readonly IDemoSessionContext _demoSession;

    public MeController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditService auditService,
        IUserService userService,
        ICurrentLanguage currentLanguage,
        IDemoSessionContext demoSession)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _auditService = auditService;
        _userService = userService;
        _currentLanguage = currentLanguage;
        _demoSession = demoSession;
    }

    /// <summary>
    /// Credential and account-lifecycle changes are refused for a demo visitor.
    ///
    /// They reached the app through /demo's "Try as [Role]" button, not a password, so there is
    /// nothing for a new password to unlock and no inbox behind the generated address. Letting
    /// these through only breaks the visit: deleting the account removes the cloned user their
    /// session is built on, so returning to /demo and picking the same role dead-ends on "role
    /// not found" with no way back short of clearing cookies.
    ///
    /// This has to be enforced here rather than only by disabling the buttons - the UI is not a
    /// security boundary, and these endpoints are reachable directly.
    ///
    /// Current is null outside demo mode and also for the owner (who signs in through /login into
    /// the template data), so neither is affected.
    /// </summary>
    private bool IsDemoVisitor => _demoSession.Current is not null;

    private ActionResult DemoNotAllowed() =>
        BadRequest(Translations.Get(_currentLanguage.Current, "errors.demo.accountLocked"));

    [HttpGet]
    public async Task<ActionResult<MyProfileDto>> GetMyProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;

        return Ok(new MyProfileDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            user.PhoneNumber,
            user.Bydlisko,
            user.RodneCislo,
            user.DateOfBirth,
            role,
            user.CreatedAt));
    }

    [HttpPut("contact")]
    public async Task<ActionResult<MyProfileDto>> UpdateMyContact([FromBody] UpdateMyContactRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound();

        // Track changes separately — Identity's SetPhoneNumberAsync has side effects (updates security stamp),
        // so it should only be called when the phone actually changed, not on every profile save.
        var phoneChanged = !string.Equals(user.PhoneNumber, request.PhoneNumber, StringComparison.Ordinal);
        var bydliskoChanged = !string.Equals(user.Bydlisko, request.Bydlisko, StringComparison.Ordinal);

        if (phoneChanged && !string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var existingWithPhone = await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber && u.Id != user.Id);
            if (existingWithPhone is not null)
                return BadRequest(Translations.Get(_currentLanguage.Current, "errors.me.phoneTaken"));
        }

        if (phoneChanged)
        {
            var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, request.PhoneNumber);
            if (!setPhoneResult.Succeeded)
                return BadRequest(string.Join("; ", setPhoneResult.Errors.Select(e => e.Description)));
        }

        if (bydliskoChanged)
        {
            user.Bydlisko = request.Bydlisko;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return BadRequest(string.Join("; ", updateResult.Errors.Select(e => e.Description)));
        }

        if (phoneChanged || bydliskoChanged)
        {
            _auditService.Log("MyContactUpdated", "User", user.Id, User,
                new { PhoneChanged = phoneChanged, BydliskoChanged = bydliskoChanged });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;

        return Ok(new MyProfileDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            user.PhoneNumber,
            user.Bydlisko,
            user.RodneCislo,
            user.DateOfBirth,
            role,
            user.CreatedAt));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangeMyPassword([FromBody] ChangeMyPasswordRequest request)
    {
        if (IsDemoVisitor)
            return DemoNotAllowed();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => TranslateIdentityError(e, _currentLanguage.Current))));

        await _signInManager.RefreshSignInAsync(user);
        _auditService.Log("MyPasswordChanged", "User", user.Id, User, null);

        return NoContent();
    }

    [HttpPost("change-email")]
    public async Task<IActionResult> ChangeMyEmail([FromBody] ChangeMyEmailRequest request)
    {
        if (IsDemoVisitor)
            return DemoNotAllowed();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound();

        if (!await _userManager.CheckPasswordAsync(user, request.CurrentPassword))
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.me.passwordMismatch"));

        var newEmail = request.NewEmail.Trim();
        if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.me.newEmailSameAsCurrent"));

        var existing = await _userManager.FindByEmailAsync(newEmail);
        if (existing is not null && existing.Id != user.Id)
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.me.duplicateEmail"));

        var setEmailResult = await _userManager.SetEmailAsync(user, newEmail);
        if (!setEmailResult.Succeeded)
            return BadRequest(string.Join("; ", setEmailResult.Errors.Select(e => TranslateIdentityError(e, _currentLanguage.Current))));

        var setUserNameResult = await _userManager.SetUserNameAsync(user, newEmail);
        if (!setUserNameResult.Succeeded)
            return BadRequest(string.Join("; ", setUserNameResult.Errors.Select(e => TranslateIdentityError(e, _currentLanguage.Current))));

        await _signInManager.RefreshSignInAsync(user);
        _auditService.Log("MyEmailChanged", "User", user.Id, User, new { NewEmail = newEmail });

        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteMyAccount([FromBody] DeleteMyAccountRequest request)
    {
        if (IsDemoVisitor)
            return DemoNotAllowed();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound();

        if (!await _userManager.CheckPasswordAsync(user, request.CurrentPassword))
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.me.passwordMismatch"));

        // Goes through UserService.DeleteUserAsync (not _userManager.DeleteAsync directly) because it
        // cleans up Restrict-FK rows first (CoachTeam, TrainingSeries.CoachId, ParentInvitation,
        // ParentChild, AttendanceRecord, AbsenceRequest) — without that cleanup, SaveChangesAsync throws
        // a raw DbUpdateException for almost any account that isn't brand new.
        try
        {
            await _userService.DeleteUserAsync(user.Id, User);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        await _signInManager.SignOutAsync();

        return NoContent();
    }

    private static string TranslateIdentityError(IdentityError error, Lang lang) => error.Code switch
    {
        "PasswordMismatch" => Translations.Get(lang, "errors.me.passwordMismatch"),
        "PasswordTooShort" => Translations.Get(lang, "errors.me.passwordTooShort"),
        "PasswordRequiresDigit" => Translations.Get(lang, "errors.me.passwordRequiresDigit"),
        "PasswordRequiresLower" => Translations.Get(lang, "errors.me.passwordRequiresLower"),
        "PasswordRequiresUpper" => Translations.Get(lang, "errors.me.passwordRequiresUpper"),
        "PasswordRequiresNonAlphanumeric" => Translations.Get(lang, "errors.me.passwordRequiresNonAlphanumeric"),
        "DuplicateEmail" => Translations.Get(lang, "errors.me.duplicateEmail"),
        "DuplicateUserName" => Translations.Get(lang, "errors.me.duplicateUserName"),
        "InvalidEmail" => Translations.Get(lang, "errors.me.invalidEmail"),
        _ => error.Description
    };
}
