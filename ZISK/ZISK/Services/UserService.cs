using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Users;

namespace ZISK.Services;

public class UserService : IUserService
{
    private static bool? _isCoachTeamsAvailable;
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

        List<CoachTeam> allCoachTeams = [];
        if (await CoachTeamsTableExistsAsync())
        {
            try
            {
                allCoachTeams = await _context.CoachTeams
                    .AsNoTracking()
                    .Include(ct => ct.Team)
                    .ToListAsync();
            }
            catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
            {
                _isCoachTeamsAvailable = false;
            }
        }

        var childProfiles = await _context.ChildProfiles
            .AsNoTracking()
            .Include(c => c.Team)
            .ToListAsync();

        var childByEmail = childProfiles
            .Where(c => !string.IsNullOrWhiteSpace(c.Email))
            .GroupBy(c => c.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

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

            if (teamName == null && userRole == "Child" && !string.IsNullOrWhiteSpace(user.Email) && childByEmail.TryGetValue(user.Email, out var childProfile))
            {
                teamName = childProfile.Team?.Name;
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

        List<UserTeamDto> teams;
        try
        {
            teams = await _context.CoachTeams
                .Where(ct => ct.CoachId == id)
                .Include(ct => ct.Team)
                .Select(ct => new UserTeamDto(ct.TeamId, ct.Team.Name, ct.IsPrimary))
                .ToListAsync();
        }
        catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "CoachTeams table is missing while loading user detail for {UserId}.", id);
            teams = [];
        }

        var parents = new List<ParentOptionDto>();
        if (userRole == "Child" && !string.IsNullOrWhiteSpace(user.Email))
        {
            var childProfile = await _context.ChildProfiles
                .Include(c => c.Team)
                .Include(c => c.Parents)
                    .ThenInclude(pc => pc.Parent)
                .FirstOrDefaultAsync(c => c.Email == user.Email);

            if (childProfile != null)
            {
                if (childProfile.Team != null && !teams.Any())
                {
                    teams.Add(new UserTeamDto(childProfile.TeamId!.Value, childProfile.Team.Name, true));
                }

                parents = childProfile.Parents
                    .Select(p => new ParentOptionDto(p.ParentId, $"{p.Parent.FirstName} {p.Parent.LastName}"))
                    .OrderBy(p => p.FullName)
                    .ToList();
            }
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

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 4)
            throw new ArgumentException("Heslo musí mať minimálne 4 znaky");

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
            throw new InvalidOperationException(result.Errors.First().Description);

        await _userManager.AddToRoleAsync(user, request.Role);

        if (request.Role == "Child")
        {
            await EnsureChildProfileAndParentsAsync(user, request.ParentIds);
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
            await EnsureChildProfileAndParentsAsync(user, request.ParentIds);
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
        List<CoachTeam> allCoachTeams = [];
        if (await CoachTeamsTableExistsAsync())
        {
            try
            {
                allCoachTeams = await _context.CoachTeams
                    .Include(ct => ct.Team)
                    .Where(ct => coachIds.Contains(ct.CoachId))
                    .ToListAsync();
            }
            catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
            {
                _isCoachTeamsAvailable = false;
            }
        }

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
        try
        {
            exists = await _context.CoachTeams.AnyAsync(ct => ct.CoachId == userId && ct.TeamId == teamId);
        }
        catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Databáza nie je synchronizovaná. Aplikujte migrácie.");
        }

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
        CoachTeam? coachTeam;
        try
        {
            coachTeam = await _context.CoachTeams
                .FirstOrDefaultAsync(ct => ct.CoachId == userId && ct.TeamId == teamId);
        }
        catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Databáza nie je synchronizovaná. Aplikujte migrácie.");
        }

        if (coachTeam == null)
            throw new KeyNotFoundException();

        _context.CoachTeams.Remove(coachTeam);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(string id, ClaimsPrincipal callingUser)
    {
        var user = await _userManager.FindByIdAsync(id)
            ?? throw new KeyNotFoundException();

        List<CoachTeam> coachLinks;
        try
        {
            coachLinks = await _context.CoachTeams.Where(ct => ct.CoachId == id).ToListAsync();
        }
        catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "CoachTeams table missing while deleting user {UserId}.", id);
            coachLinks = [];
        }
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

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var childProfile = await _context.ChildProfiles.FirstOrDefaultAsync(c => c.Email == user.Email);
            if (childProfile != null)
            {
                var attendance = await _context.AttendanceRecords.Where(a => a.ChildId == childProfile.Id).ToListAsync();
                var absences = await _context.AbsenceRequests.Where(a => a.ChildId == childProfile.Id).ToListAsync();
                var links = await _context.ParentChildren.Where(pc => pc.ChildId == childProfile.Id).ToListAsync();

                _context.AttendanceRecords.RemoveRange(attendance);
                _context.AbsenceRequests.RemoveRange(absences);
                _context.ParentChildren.RemoveRange(links);
                _context.ChildProfiles.Remove(childProfile);
            }
        }

        await _context.SaveChangesAsync();

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Errors.First().Description);

        _auditService.Log("Delete", "User", id, callingUser, null);
    }

    private async Task UpdateCoachTeamsAsync(string id, List<Guid> teamIds, ClaimsPrincipal callingUser, ApplicationUser user)
    {
        try
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
        catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "CoachTeams table missing while updating teams for {UserId}. Trying to create.", id);
            await EnsureCoachTeamsTableAsync();

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
    }

    private async Task EnsureCoachTeamsTableAsync()
    {
        await _context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[CoachTeams]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CoachTeams](
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [CoachId] NVARCHAR(450) NOT NULL,
        [TeamId] UNIQUEIDENTIFIER NOT NULL,
        [IsPrimary] BIT NOT NULL CONSTRAINT [DF_CoachTeams_IsPrimary] DEFAULT(0),
        [AssignedAt] DATETIME2 NOT NULL CONSTRAINT [DF_CoachTeams_AssignedAt] DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT [PK_CoachTeams] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CoachTeams_AspNetUsers_CoachId] FOREIGN KEY ([CoachId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CoachTeams_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_CoachTeams_CoachId] ON [dbo].[CoachTeams]([CoachId]);
    CREATE INDEX [IX_CoachTeams_TeamId] ON [dbo].[CoachTeams]([TeamId]);
END");

        _isCoachTeamsAvailable = true;
    }

    private async Task EnsureChildProfileAndParentsAsync(ApplicationUser user, List<string>? parentIds)
    {
        if (string.IsNullOrWhiteSpace(user.Email) || user.DateOfBirth is null)
            return;

        var childProfile = await _context.ChildProfiles
            .Include(c => c.Parents)
            .FirstOrDefaultAsync(c => c.Email == user.Email);

        if (childProfile == null)
        {
            childProfile = new ChildProfile
            {
                Id = Guid.NewGuid(),
                FirstName = user.FirstName,
                LastName = user.LastName,
                DateOfBirth = user.DateOfBirth.Value,
                Email = user.Email,
                IsActive = user.IsActive,
                CreatedAt = DateTime.UtcNow
            };
            _context.ChildProfiles.Add(childProfile);
        }
        else
        {
            childProfile.FirstName = user.FirstName;
            childProfile.LastName = user.LastName;
            childProfile.DateOfBirth = user.DateOfBirth.Value;
            childProfile.Email = user.Email;
            childProfile.IsActive = user.IsActive;
        }

        if (parentIds != null)
        {
            var distinctParentIds = parentIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var existingLinks = await _context.ParentChildren.Where(pc => pc.ChildId == childProfile.Id).ToListAsync();
            _context.ParentChildren.RemoveRange(existingLinks);

            for (int i = 0; i < distinctParentIds.Count; i++)
            {
                _context.ParentChildren.Add(new ParentChild
                {
                    ParentId = distinctParentIds[i],
                    ChildId = childProfile.Id,
                    IsPrimary = i == 0
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task<bool> CoachTeamsTableExistsAsync()
    {
        if (_isCoachTeamsAvailable.HasValue)
            return _isCoachTeamsAvailable.Value;

        try
        {
            var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync();
            if (!appliedMigrations.Any(m => m.Contains("CreateIdentitySchema", StringComparison.OrdinalIgnoreCase)))
            {
                _isCoachTeamsAvailable = false;
                return false;
            }

            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose) await connection.OpenAsync();

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'[dbo].[CoachTeams]', N'U') IS NULL THEN 0 ELSE 1 END";
                var result = await command.ExecuteScalarAsync();
                _isCoachTeamsAvailable = Convert.ToInt32(result) == 1;
                return _isCoachTeamsAvailable.Value;
            }
            finally
            {
                if (shouldClose) await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to verify CoachTeams table existence.");
            _isCoachTeamsAvailable = false;
            return false;
        }
    }
}
