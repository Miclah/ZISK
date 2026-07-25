using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZISK.Data;
using ZISK.Services;
using ZISK.Shared.DTOs.Users;

namespace ZISK.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditService _auditService;

    public MeController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditService auditService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _auditService = auditService;
    }

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
                return BadRequest("Toto telefónne číslo už používa iný účet.");
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => TranslateIdentityError(e))));

        await _signInManager.RefreshSignInAsync(user);
        _auditService.Log("MyPasswordChanged", "User", user.Id, User, null);

        return NoContent();
    }

    [HttpPost("change-email")]
    public async Task<IActionResult> ChangeMyEmail([FromBody] ChangeMyEmailRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound();

        if (!await _userManager.CheckPasswordAsync(user, request.CurrentPassword))
            return BadRequest("Súčasné heslo nie je správne.");

        var newEmail = request.NewEmail.Trim();
        if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Nový email je rovnaký ako súčasný.");

        var existing = await _userManager.FindByEmailAsync(newEmail);
        if (existing is not null && existing.Id != user.Id)
            return BadRequest("Tento email už používa iný účet.");

        var setEmailResult = await _userManager.SetEmailAsync(user, newEmail);
        if (!setEmailResult.Succeeded)
            return BadRequest(string.Join("; ", setEmailResult.Errors.Select(TranslateIdentityError)));

        var setUserNameResult = await _userManager.SetUserNameAsync(user, newEmail);
        if (!setUserNameResult.Succeeded)
            return BadRequest(string.Join("; ", setUserNameResult.Errors.Select(TranslateIdentityError)));

        await _signInManager.RefreshSignInAsync(user);
        _auditService.Log("MyEmailChanged", "User", user.Id, User, new { NewEmail = newEmail });

        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteMyAccount([FromBody] DeleteMyAccountRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound();

        if (!await _userManager.CheckPasswordAsync(user, request.CurrentPassword))
            return BadRequest("Súčasné heslo nie je správne.");

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(TranslateIdentityError)));

        await _signInManager.SignOutAsync();
        _auditService.Log("MyAccountDeleted", "User", user.Id, User, null);

        return NoContent();
    }

    private static string TranslateIdentityError(IdentityError error) => error.Code switch
    {
        "PasswordMismatch" => "Súčasné heslo nie je správne.",
        "PasswordTooShort" => "Heslo je príliš krátke (min. 8 znakov).",
        "PasswordRequiresDigit" => "Heslo musí obsahovať aspoň jednu číslicu.",
        "PasswordRequiresLower" => "Heslo musí obsahovať aspoň jedno malé písmeno.",
        "PasswordRequiresUpper" => "Heslo musí obsahovať aspoň jedno veľké písmeno.",
        "PasswordRequiresNonAlphanumeric" => "Heslo musí obsahovať aspoň jeden špeciálny znak.",
        "DuplicateEmail" => "Tento email už používa iný účet.",
        "DuplicateUserName" => "Toto používateľské meno je už obsadené.",
        "InvalidEmail" => "Neplatný formát emailu.",
        _ => error.Description
    };
}
