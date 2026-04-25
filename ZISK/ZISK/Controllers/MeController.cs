using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IAuditService _auditService;

    public MeController(UserManager<ApplicationUser> userManager, IAuditService auditService)
    {
        _userManager = userManager;
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

        var phoneChanged = !string.Equals(user.PhoneNumber, request.PhoneNumber, StringComparison.Ordinal);
        var bydliskoChanged = !string.Equals(user.Bydlisko, request.Bydlisko, StringComparison.Ordinal);

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
}
