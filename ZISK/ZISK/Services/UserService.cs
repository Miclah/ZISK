using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Users;

namespace ZISK.Services;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        ILogger<UserService> logger)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<List<UserListDto>> GetUsersAsync(string? role)
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();

        var userRoles = await _context.UserRoles.AsNoTracking().ToListAsync();
        var roles = await _context.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name);
        var userRoleMap = userRoles
            .GroupBy(ur => ur.UserId)
            .ToDictionary(g => g.Key, g => g.Select(ur => roles.TryGetValue(ur.RoleId, out var name) ? name : null).FirstOrDefault() ?? "Parent");

        var allCoachTeams = await _context.CoachTeams
            .AsNoTracking()
            .Include(ct => ct.Team)
            .ToListAsync();

        var allTeamMemberships = await _context.TeamMembers
            .AsNoTracking()
            .Include(tm => tm.Team)
            .ToListAsync();

        var result = new List<UserListDto>();

        foreach (var user in users)
        {
            var userRole = userRoleMap.TryGetValue(user.Id, out var r) ? r : "Parent";

            if (!string.IsNullOrWhiteSpace(role) && userRole != role)
                continue;

            var userTeams = allCoachTeams
                .Where(ct => ct.CoachId == user.Id)
                .Select(ct => new UserTeamDto(ct.TeamId, ct.Team.Name, ct.IsPrimary))
                .ToList();

            string? teamName = userTeams
                .OrderByDescending(t => t.IsPrimary)
                .Select(t => t.TeamName)
                .FirstOrDefault();

            if (teamName == null && (userRole == "Child" || userRole == "Athlete"))
            {
                teamName = allTeamMemberships
                    .FirstOrDefault(tm => tm.UserId == user.Id)?.Team.Name;
            }

            result.Add(new UserListDto(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email ?? string.Empty,
                user.PhoneNumber,
                userRole,
                user.IsActive,
                user.CreatedAt,
                teamName,
                userTeams
            ));
        }

        return result;
    }

    public async Task<UserDto> GetUserAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id)
            ?? throw new KeyNotFoundException();

        var userRoles = await _userManager.GetRolesAsync(user);
        var userRole = userRoles.FirstOrDefault() ?? "Parent";

        var teams = await _context.CoachTeams
            .Where(ct => ct.CoachId == id)
            .Include(ct => ct.Team)
            .Select(ct => new UserTeamDto(ct.TeamId, ct.Team.Name, ct.IsPrimary))
            .ToListAsync();

        var parents = new List<ParentOptionDto>();
        if (userRole == "Child" || userRole == "Athlete")
        {
            // Get team via TeamMember
            if (!teams.Any())
            {
                var membership = await _context.TeamMembers
                    .Include(tm => tm.Team)
                    .FirstOrDefaultAsync(tm => tm.UserId == id);
                if (membership != null)
                    teams.Add(new UserTeamDto(membership.TeamId, membership.Team.Name, true));
            }

            // Get parents via ParentChildren
            var parentRows = await _context.ParentChildren
                .Include(pc => pc.Parent)
                .Where(pc => pc.ChildId == id)
                .Select(p => new { p.ParentId, p.Parent.FirstName, p.Parent.LastName })
                .ToListAsync();
            parents = parentRows
                .Select(p => new ParentOptionDto(p.ParentId, $"{p.FirstName} {p.LastName}"))
                .OrderBy(p => p.FullName)
                .ToList();
        }

        return new UserDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            user.PhoneNumber,
            userRole,
            user.IsActive,
            user.CreatedAt,
            user.RodneCislo,
            user.Bydlisko,
            user.DateOfBirth,
            teams,
            parents
        );
    }

    public async Task<List<ParentOptionDto>> GetParentsAsync()
    {
        var parents = await _userManager.GetUsersInRoleAsync("Parent");
        return parents
            .Where(p => p.IsActive)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Select(p => new ParentOptionDto(p.Id, $"{p.FirstName} {p.LastName}"))
            .ToList();
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, ClaimsPrincipal callingUser)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Length < 2 || request.FirstName.Length > 100)
            throw new ArgumentException("Meno musí mať 2-100 znakov");

        if (string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Length < 2 || request.LastName.Length > 100)
            throw new ArgumentException("Priezvisko musí mať 2-100 znakov");

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new ArgumentException("Neplatný email");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new ArgumentException("Heslo musí mať minimálne 8 znakov");

        var validRoles = new[] { "Admin", "Coach", "Parent", "Athlete", "Child" };
        if (!validRoles.Contains(request.Role))
            throw new ArgumentException("Neplatná rola");

        if (request.Role == "Child" && request.DateOfBirth is null)
            throw new ArgumentException("Pre Dieťa je dátum narodenia povinný");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            RodneCislo = request.RodneCislo,
            Bydlisko = request.Bydlisko,
            DateOfBirth = request.DateOfBirth,
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(IdentityErrorLocalizer.LocalizeFirst(result.Errors));

        await _userManager.AddToRoleAsync(user, request.Role);

        if (request.Role == "Child")
        {
            await EnsureChildParentLinksAsync(user, request.ParentIds);
        }

        _auditService.Log("Create", "User", user.Id, callingUser, new { request.Email, request.Role });
        return await GetUserAsync(user.Id);
    }

    public async Task<UserDto> UpdateUserAsync(string id, UpdateUserRequest request, ClaimsPrincipal callingUser)
    {
        var user = await _userManager.FindByIdAsync(id)
            ?? throw new KeyNotFoundException();

        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;
        if (request.PhoneNumber != null) user.PhoneNumber = request.PhoneNumber;
        if (request.RodneCislo != null) user.RodneCislo = request.RodneCislo;
        if (request.Bydlisko != null) user.Bydlisko = request.Bydlisko;
        if (request.DateOfBirth.HasValue) user.DateOfBirth = request.DateOfBirth.Value;

        await _userManager.UpdateAsync(user);

        var currentRoles = await _userManager.GetRolesAsync(user);
        var currentRole = currentRoles.FirstOrDefault() ?? "Parent";
        var effectiveRole = currentRole;

        if (request.Role != null)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, request.Role);
            effectiveRole = request.Role;
            _auditService.Log("RoleChanged", "User", user.Id, callingUser, new { From = currentRole, To = request.Role });
        }

        if (request.TeamIds != null)
        {
            await UpdateCoachTeamsAsync(id, request.TeamIds, callingUser, user);
        }

        if (effectiveRole == "Child")
        {
            if (user.DateOfBirth is null)
                throw new ArgumentException("Pre Dieťa je dátum narodenia povinný");
            await EnsureChildParentLinksAsync(user, request.ParentIds);
        }

        _auditService.Log("Update", "User", user.Id, callingUser, new
        {
            request.FirstName, request.LastName, request.IsActive,
            request.PhoneNumber, request.RodneCislo, request.Bydlisko, request.DateOfBirth
        });

        return await GetUserAsync(id);
    }

    public async Task ToggleStatusAsync(string id, ClaimsPrincipal callingUser)
    {
        var user = await _userManager.FindByIdAsync(id)
            ?? throw new KeyNotFoundException();

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);
        _auditService.Log("ToggleStatus", "User", user.Id, callingUser, new { user.IsActive });
    }

    public async Task<List<UserListDto>> GetCoachesAsync()
    {
        var coaches = await _userManager.GetUsersInRoleAsync("Coach");

        var coachIds = coaches.Select(c => c.Id).ToList();
        var allCoachTeams = await _context.CoachTeams
            .Include(ct => ct.Team)
            .Where(ct => coachIds.Contains(ct.CoachId))
            .ToListAsync();

        return coaches.Select(u =>
        {
            var userTeams = allCoachTeams
                .Where(ct => ct.CoachId == u.Id)
                .Select(ct => new UserTeamDto(ct.TeamId, ct.Team.Name, ct.IsPrimary))
                .ToList();

            return new UserListDto(
                u.Id, u.FirstName, u.LastName, u.Email ?? string.Empty, u.PhoneNumber,
                "Coach", u.IsActive, u.CreatedAt,
                userTeams.OrderByDescending(t => t.IsPrimary).Select(t => t.TeamName).FirstOrDefault(),
                userTeams
            );
        }).ToList();
    }

    public async Task AssignTeamAsync(string userId, Guid teamId, bool isPrimary)
    {
        bool exists;
        exists = await _context.CoachTeams.AnyAsync(ct => ct.CoachId == userId && ct.TeamId == teamId);

        if (exists)
            throw new InvalidOperationException("Tím je už priradený");

        if (isPrimary)
        {
            var existing = await _context.CoachTeams
                .Where(ct => ct.CoachId == userId && ct.IsPrimary)
                .ToListAsync();
            foreach (var ct in existing)
                ct.IsPrimary = false;
        }

        _context.CoachTeams.Add(new CoachTeam
        {
            CoachId = userId,
            TeamId = teamId,
            IsPrimary = isPrimary
        });

        await _context.SaveChangesAsync();
    }

    public async Task RemoveTeamAsync(string userId, Guid teamId)
    {
        var coachTeam = await _context.CoachTeams
            .FirstOrDefaultAsync(ct => ct.CoachId == userId && ct.TeamId == teamId);

        if (coachTeam == null)
            throw new KeyNotFoundException();

        _context.CoachTeams.Remove(coachTeam);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(string id, ClaimsPrincipal callingUser)
    {
        var user = await _userManager.FindByIdAsync(id)
            ?? throw new KeyNotFoundException();

        var coachLinks = await _context.CoachTeams.Where(ct => ct.CoachId == id).ToListAsync();
        if (coachLinks.Count > 0)
            _context.CoachTeams.RemoveRange(coachLinks);

        var announcements = await _context.Announcements
            .Include(a => a.Attachments)
            .Where(a => a.AuthorUserId == id)
            .ToListAsync();

        foreach (var announcement in announcements)
            _context.AnnouncementAttachments.RemoveRange(announcement.Attachments);

        if (announcements.Count > 0)
            _context.Announcements.RemoveRange(announcements);

        var sentAbsences = await _context.AbsenceRequests.Where(a => a.ParentId == id).ToListAsync();
        if (sentAbsences.Count > 0)
            _context.AbsenceRequests.RemoveRange(sentAbsences);

        // Remove child-related data (AttendanceRecords, AbsenceRequests as child, ParentChildren, TeamMember)
        var attendance = await _context.AttendanceRecords.Where(a => a.ChildId == id).ToListAsync();
        if (attendance.Count > 0) _context.AttendanceRecords.RemoveRange(attendance);

        var absencesAsChild = await _context.AbsenceRequests.Where(a => a.ChildId == id).ToListAsync();
        if (absencesAsChild.Count > 0) _context.AbsenceRequests.RemoveRange(absencesAsChild);

        var parentLinks = await _context.ParentChildren.Where(pc => pc.ChildId == id || pc.ParentId == id).ToListAsync();
        if (parentLinks.Count > 0) _context.ParentChildren.RemoveRange(parentLinks);

        var teamMemberships = await _context.TeamMembers.Where(tm => tm.UserId == id).ToListAsync();
        if (teamMemberships.Count > 0) _context.TeamMembers.RemoveRange(teamMemberships);

        await _context.SaveChangesAsync();

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(IdentityErrorLocalizer.LocalizeFirst(result.Errors));

        _auditService.Log("Delete", "User", id, callingUser, null);
    }

    private async Task UpdateCoachTeamsAsync(string id, List<Guid> teamIds, ClaimsPrincipal callingUser, ApplicationUser user)
    {
        var existingTeams = await _context.CoachTeams.Where(ct => ct.CoachId == id).ToListAsync();
        _context.CoachTeams.RemoveRange(existingTeams);

        for (int i = 0; i < teamIds.Count; i++)
        {
            _context.CoachTeams.Add(new CoachTeam
            {
                CoachId = id,
                TeamId = teamIds[i],
                IsPrimary = i == 0
            });
        }

        await _context.SaveChangesAsync();
        _auditService.Log("TeamAssignmentUpdated", "User", user.Id, callingUser, new { TeamCount = teamIds.Count });
    }

    private async Task EnsureChildParentLinksAsync(ApplicationUser user, List<string>? parentIds)
    {
        if (parentIds == null)
            return;

        var distinctParentIds = parentIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var existingLinks = await _context.ParentChildren.Where(pc => pc.ChildId == user.Id).ToListAsync();
        _context.ParentChildren.RemoveRange(existingLinks);

        for (int i = 0; i < distinctParentIds.Count; i++)
        {
            _context.ParentChildren.Add(new ParentChild
            {
                ParentId = distinctParentIds[i],
                ChildId = user.Id,
                IsPrimary = i == 0
            });
        }

        await _context.SaveChangesAsync();
    }

}
