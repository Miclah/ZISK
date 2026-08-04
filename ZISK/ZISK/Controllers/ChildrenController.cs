using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZISK.Data;
using ZISK.Services;
using ZISK.Shared.DTOs.Children;
using ZISK.Shared.Localization;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChildrenController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UsernameGenerator _usernameGenerator;
    private readonly IAuditService _auditService;
    private readonly SmtpEmailSender _emailSender;
    private readonly ITeamAccessService _teamAccessService;
    private readonly ILogger<ChildrenController> _logger;
    private readonly ICurrentLanguage _currentLanguage;

    public ChildrenController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        UsernameGenerator usernameGenerator,
        IAuditService auditService,
        SmtpEmailSender emailSender,
        ITeamAccessService teamAccessService,
        ILogger<ChildrenController> logger,
        ICurrentLanguage currentLanguage)
    {
        _context = context;
        _userManager = userManager;
        _usernameGenerator = usernameGenerator;
        _auditService = auditService;
        _emailSender = emailSender;
        _teamAccessService = teamAccessService;
        _logger = logger;
        _currentLanguage = currentLanguage;
    }

    [HttpGet("my")]
    public async Task<ActionResult<List<ChildDto>>> GetMyChildren()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = new List<ChildDto>();

        // Athlete and Child roles return themselves as the single "child" entry — they are not a parent,
        // but the same endpoint is used. IsOwnProfile=true lets the UI distinguish this case.
        if (User.IsInRole("Athlete") || User.IsInRole("Child"))
        {
            var membership = await _context.TeamMembers
                .Include(tm => tm.Team)
                .FirstOrDefaultAsync(tm => tm.UserId == userId);

            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                result.Add(new ChildDto(
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    membership?.TeamId,
                    membership?.Team.Name,
                    true,
                    user.DateOfBirth
                ));
            }
        }

        if (User.IsInRole("Parent"))
        {
            var children = await _context.ParentChildren
                .Include(pc => pc.Child)
                .Where(pc => pc.ParentId == userId)
                .ToListAsync();

            foreach (var pc in children)
            {
                var membership = await _context.TeamMembers
                    .Include(tm => tm.Team)
                    .FirstOrDefaultAsync(tm => tm.UserId == pc.ChildId);

                result.Add(new ChildDto(
                    pc.Child.Id,
                    pc.Child.FirstName,
                    pc.Child.LastName,
                    membership?.TeamId,
                    membership?.Team.Name,
                    false,
                    pc.Child.DateOfBirth
                ));
            }
        }

        // GroupBy deduplicates entries for a user who has both Athlete and Parent roles at the same time.
        return Ok(result
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToList());
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<ActionResult<List<ChildDto>>> GetAllChildren()
    {
        var childRoleIds = await _context.Roles
            .Where(r => r.Name == "Child" || r.Name == "Athlete")
            .Select(r => r.Id)
            .ToListAsync();

        if (!childRoleIds.Any())
            return Ok(new List<ChildDto>());

        var childUserIds = await _context.UserRoles
            .Where(ur => childRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync();

        var memberships = await _context.TeamMembers
            .Include(tm => tm.Team)
            .Where(tm => childUserIds.Contains(tm.UserId))
            .ToListAsync();

        var children = await _context.Users
            .Where(u => childUserIds.Contains(u.Id) && u.IsActive)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();

        var result = children.Select(u =>
        {
            var m = memberships.FirstOrDefault(tm => tm.UserId == u.Id);
            return new ChildDto(u.Id, u.FirstName, u.LastName, m?.TeamId, m?.Team.Name, false, u.DateOfBirth);
        }).ToList();

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Parent,Admin")]
    public async Task<ActionResult<ChildDto>> CreateChild([FromBody] CreateChildRequest request)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(callerId))
            return Unauthorized();

        // Validate age < 18
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.DateOfBirth > today.AddYears(-0) || request.DateOfBirth <= today.AddYears(-18)) // AddYears(-0) == today; rejects future dates
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.child.mustBeUnder18"));

        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Length < 2)
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.child.firstNameMinLength2"));

        if (string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Length < 2)
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.child.lastNameMinLength2"));

        if (request.CreateCredentials)
        {
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                return BadRequest(Translations.Get(_currentLanguage.Current, "errors.child.passwordMinLength6"));
        }

        string bydlisko = request.Bydlisko ?? string.Empty;
        if (request.SameAddressAsParent && User.IsInRole("Parent"))
        {
            var caller = await _context.Users.FindAsync(callerId);
            bydlisko = caller?.Bydlisko ?? string.Empty;
        }

        var username = !string.IsNullOrWhiteSpace(request.OverrideUsername)
            ? request.OverrideUsername
            : await _usernameGenerator.GenerateAsync(request.FirstName, request.LastName);

        var child = new ApplicationUser
        {
            UserName = username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Bydlisko = bydlisko,
            EmailConfirmed = true,
            IsActive = true
        };

        IdentityResult createResult;
        // A child account can exist without login credentials — the parent manages it on their behalf.
        if (request.CreateCredentials && !string.IsNullOrWhiteSpace(request.Password))
            createResult = await _userManager.CreateAsync(child, request.Password);
        else
            createResult = await _userManager.CreateAsync(child);

        if (!createResult.Succeeded)
            return BadRequest(string.Join("; ", createResult.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(child, "Child");

        _context.ParentChildren.Add(new ParentChild
        {
            ParentId = callerId,
            ChildId = child.Id,
            IsPrimary = true
        });

        if (request.TeamId.HasValue)
        {
            var teamExists = await _context.Teams.AnyAsync(t => t.Id == request.TeamId.Value);
            if (teamExists)
            {
                _context.TeamMembers.Add(new Data.Entities.TeamMember
                {
                    TeamId = request.TeamId.Value,
                    UserId = child.Id
                });
            }
        }

        await _context.SaveChangesAsync();

        _auditService.Log("ChildCreatedByParent", "User", child.Id, User,
            new { childName = $"{child.FirstName} {child.LastName}", request.TeamId });

        // Notify admin
        var adminRoleId = await _context.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).FirstOrDefaultAsync();
        if (adminRoleId != null)
        {
            var adminIds = await _context.UserRoles.Where(ur => ur.RoleId == adminRoleId).Select(ur => ur.UserId).ToListAsync();
            var admins = await _context.Users.Where(u => adminIds.Contains(u.Id) && u.Email != null).ToListAsync();
            var callerUser = await _context.Users.FindAsync(callerId);
            var parentName = callerUser != null ? $"{callerUser.FirstName} {callerUser.LastName}" : callerId;
            var childName = $"{child.FirstName} {child.LastName}";

            foreach (var admin in admins)
            {
                try { await _emailSender.SendChildAddedNotificationAsync(admin.Email!, childName, parentName); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to notify admin {AdminId}", admin.Id); }
            }
        }

        var membership2 = request.TeamId.HasValue
            ? await _context.TeamMembers.Include(tm => tm.Team).FirstOrDefaultAsync(tm => tm.UserId == child.Id)
            : null;

        return Ok(new ChildDto(child.Id, child.FirstName, child.LastName, membership2?.TeamId, membership2?.Team.Name, false, child.DateOfBirth));
    }

    [HttpGet("{childId}")]
    public async Task<ActionResult<ChildDetailDto>> GetChild(string childId)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(callerId))
            return Unauthorized();

        var child = await _context.Users.FirstOrDefaultAsync(u => u.Id == childId);
        if (child == null)
            return NotFound();

        if (!await CanAccessChildAsync(childId, callerId))
            return Forbid();

        var membership = await _context.TeamMembers
            .Include(tm => tm.Team)
            .FirstOrDefaultAsync(tm => tm.UserId == childId);

        return Ok(new ChildDetailDto(
            child.Id,
            child.FirstName,
            child.LastName,
            child.DateOfBirth,
            child.Bydlisko,
            membership?.TeamId,
            membership?.Team.Name));
    }

    [HttpPut("{childId}")]
    public async Task<ActionResult<ChildDetailDto>> UpdateChild(string childId, [FromBody] UpdateChildRequest request)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(callerId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Length < 2)
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.child.firstNameMinLength2"));
        if (string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Length < 2)
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.child.lastNameMinLength2"));

        var child = await _context.Users.FirstOrDefaultAsync(u => u.Id == childId);
        if (child == null)
            return NotFound();

        var isAdmin = User.IsInRole("Admin");
        var isCoach = User.IsInRole("Coach");
        var isParent = User.IsInRole("Parent");

        if (!await CanAccessChildAsync(childId, callerId))
            return Forbid();

        var currentMembership = await _context.TeamMembers
            .FirstOrDefaultAsync(tm => tm.UserId == childId);
        var currentTeamId = currentMembership?.TeamId;

        bool teamChangeRequested = request.TeamId != currentTeamId;

        // Two-step team change validation: (1) Parent cannot change teams at all;
        // (2) Coach can only change to/from teams they have access to. Order matters — Parent check is first.
        if (teamChangeRequested && isParent && !isAdmin && !isCoach)
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.child.onlyCoachOrAdminCanChangeTeam"));

        if (teamChangeRequested && isCoach && !isAdmin)
        {
            var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(User);
            if (request.TeamId.HasValue && accessibleTeamIds is not null && !accessibleTeamIds.Contains(request.TeamId.Value))
                return Forbid();
            if (currentTeamId.HasValue && accessibleTeamIds is not null && !accessibleTeamIds.Contains(currentTeamId.Value))
                return Forbid();
        }

        child.FirstName = request.FirstName;
        child.LastName = request.LastName;
        child.Bydlisko = request.Bydlisko ?? string.Empty;
        child.DateOfBirth = request.DateOfBirth;

        if (teamChangeRequested)
        {
            if (currentMembership != null)
                _context.TeamMembers.Remove(currentMembership);

            if (request.TeamId.HasValue)
            {
                var teamExists = await _context.Teams.AnyAsync(t => t.Id == request.TeamId.Value);
                if (!teamExists)
                    return BadRequest(Translations.Get(_currentLanguage.Current, "errors.child.selectedTeamNotFound"));

                _context.TeamMembers.Add(new Data.Entities.TeamMember
                {
                    TeamId = request.TeamId.Value,
                    UserId = childId,
                    JoinedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();

        _auditService.Log("ChildUpdated", "User", childId, User,
            new { request.FirstName, request.LastName, request.TeamId, TeamChanged = teamChangeRequested });

        var newMembership = await _context.TeamMembers
            .Include(tm => tm.Team)
            .FirstOrDefaultAsync(tm => tm.UserId == childId);

        return Ok(new ChildDetailDto(
            child.Id, child.FirstName, child.LastName, child.DateOfBirth,
            child.Bydlisko, newMembership?.TeamId, newMembership?.Team.Name));
    }

    private async Task<bool> CanAccessChildAsync(string childId, string callerId)
    {
        if (User.IsInRole("Admin"))
            return true;

        if (User.IsInRole("Parent"))
        {
            return await _context.ParentChildren
                .AnyAsync(pc => pc.ParentId == callerId && pc.ChildId == childId);
        }

        if (User.IsInRole("Coach"))
        {
            var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(User);
            if (accessibleTeamIds is null) // null = Admin by TeamAccessService convention
                return true;

            var childTeamIds = await _context.TeamMembers
                .Where(tm => tm.UserId == childId)
                .Select(tm => tm.TeamId)
                .ToListAsync();

            return childTeamIds.Any(id => accessibleTeamIds.Contains(id));
        }

        return false;
    }

    [HttpGet("{childId}/parents")]
    public async Task<ActionResult<List<ParentDto>>> GetParents(string childId)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(callerId))
            return Unauthorized();

        if (!User.IsInRole("Admin"))
        {
            var isParent = await _context.ParentChildren.AnyAsync(pc => pc.ParentId == callerId && pc.ChildId == childId);
            if (!isParent)
                return Forbid();
        }

        var parents = await _context.ParentChildren
            .Include(pc => pc.Parent)
            .Where(pc => pc.ChildId == childId)
            .OrderByDescending(pc => pc.IsPrimary)
            .ThenBy(pc => pc.Parent.LastName)
            .Select(pc => new ParentDto(
                pc.Parent.Id,
                pc.Parent.FirstName,
                pc.Parent.LastName,
                pc.Parent.Email ?? string.Empty,
                pc.Parent.CreatedAt,
                pc.IsPrimary))
            .ToListAsync();

        return Ok(parents);
    }

    [HttpDelete("{childId}/parents/{parentUserId}")]
    public async Task<IActionResult> RemoveParent(string childId, string parentUserId)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(callerId))
            return Unauthorized();

        if (!User.IsInRole("Admin") && callerId != parentUserId)
            return Forbid();

        var link = await _context.ParentChildren.FirstOrDefaultAsync(pc => pc.ParentId == parentUserId && pc.ChildId == childId);
        if (link == null)
            return NotFound();

        var totalParents = await _context.ParentChildren.CountAsync(pc => pc.ChildId == childId);
        if (totalParents <= 1)
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.child.lastParentCannotBeRemoved"));

        _context.ParentChildren.Remove(link);
        await _context.SaveChangesAsync();

        _auditService.Log("ParentRemoved", "ParentChild", childId, User, new { parentUserId });
        return Ok();
    }
}

public record ChildDto(string Id, string FirstName, string LastName, Guid? TeamId, string? TeamName, bool IsOwnProfile, DateOnly? DateOfBirth = null);
