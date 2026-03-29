using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;
using ZISK.Shared.DTOs.Users;

namespace ZISK.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService,
            ILogger<UsersController> logger)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<UserListDto>>> GetUsers([FromQuery] string? role = null)
        {
            var users = await _userManager.Users
                .AsNoTracking()
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            List<CoachTeam> allCoachTeams = [];
            if (await CoachTeamsTableExistsAsync())
            {
                allCoachTeams = await _context.CoachTeams
                    .AsNoTracking()
                    .Include(ct => ct.Team)
                    .ToListAsync();
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
                var roles = await _userManager.GetRolesAsync(user);
                var userRole = roles.FirstOrDefault() ?? "Parent";

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

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var userRole = roles.FirstOrDefault() ?? "Parent";

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
                _logger.LogWarning(ex, "CoachTeams table is missing in database while loading user detail for {UserId}.", id);
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

            return Ok(new UserDto(
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
            ));
        }

        [HttpGet("parents")]
        public async Task<ActionResult<List<ParentOptionDto>>> GetParents()
        {
            var parents = await _userManager.GetUsersInRoleAsync("Parent");
            var result = parents
                .Where(p => p.IsActive)
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .Select(p => new ParentOptionDto(p.Id, $"{p.FirstName} {p.LastName}"))
                .ToList();

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<UserDto>> CreateUser(CreateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Length < 2 || request.FirstName.Length > 100)
                return BadRequest("Meno musí mať 2-100 znakov");

            if (string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Length < 2 || request.LastName.Length > 100)
                return BadRequest("Priezvisko musí mať 2-100 znakov");

            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
                return BadRequest("Neplatný email");

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 4)
                return BadRequest("Heslo musí mať minimálne 4 znaky");

            var validRoles = new[] { "Admin", "Coach", "Parent", "Athlete", "Child" };
            if (!validRoles.Contains(request.Role))
                return BadRequest("Neplatná rola");

            if (request.Role == "Child" && request.DateOfBirth is null)
                return BadRequest("Pre Dieťa je dátum narodenia povinný");

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
                return BadRequest(result.Errors.First().Description);

            await _userManager.AddToRoleAsync(user, request.Role);

            if (request.Role == "Child")
            {
                await EnsureChildProfileAndParentsAsync(user, request.ParentIds);
            }

            _auditService.Log("Create", "User", user.Id, User, new { request.Email, request.Role });
            return await GetUser(user.Id);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<UserDto>> UpdateUser(string id, UpdateUserRequest request)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            if (request.FirstName != null)
                user.FirstName = request.FirstName;
            if (request.LastName != null)
                user.LastName = request.LastName;
            if (request.IsActive.HasValue)
                user.IsActive = request.IsActive.Value;
            if (request.PhoneNumber != null)
                user.PhoneNumber = request.PhoneNumber;
            if (request.RodneCislo != null)
                user.RodneCislo = request.RodneCislo;
            if (request.Bydlisko != null)
                user.Bydlisko = request.Bydlisko;
            if (request.DateOfBirth.HasValue)
                user.DateOfBirth = request.DateOfBirth.Value;

            await _userManager.UpdateAsync(user);

            var currentRoles = await _userManager.GetRolesAsync(user);
            var currentRole = currentRoles.FirstOrDefault() ?? "Parent";
            var effectiveRole = currentRole;

            if (request.Role != null)
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, request.Role);
                effectiveRole = request.Role;
                _auditService.Log("RoleChanged", "User", user.Id, User, new { From = currentRole, To = request.Role });
            }

            if (request.TeamIds != null)
            {
                try
                {
                    var existingTeams = await _context.CoachTeams.Where(ct => ct.CoachId == id).ToListAsync();
                    _context.CoachTeams.RemoveRange(existingTeams);

                    for (int i = 0; i < request.TeamIds.Count; i++)
                    {
                        _context.CoachTeams.Add(new CoachTeam
                        {
                            CoachId = id,
                            TeamId = request.TeamIds[i],
                            IsPrimary = i == 0
                        });
                    }

                    await _context.SaveChangesAsync();
                    _auditService.Log("TeamAssignmentUpdated", "User", user.Id, User, new { TeamCount = request.TeamIds.Count });
                }
                catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError(ex, "CoachTeams table is missing in database while updating team assignments for {UserId}.", id);
                    return StatusCode(500, "Databáza nie je synchronizovaná. Reštartujte aplikáciu a aplikujte migrácie.");
                }
            }

            if (effectiveRole == "Child")
            {
                if (user.DateOfBirth is null)
                    return BadRequest("Pre Dieťa je dátum narodenia povinný");

                await EnsureChildProfileAndParentsAsync(user, request.ParentIds);
            }

            _auditService.Log("Update", "User", user.Id, User, new
            {
                request.FirstName,
                request.LastName,
                request.IsActive,
                request.PhoneNumber,
                request.RodneCislo,
                request.Bydlisko,
                request.DateOfBirth
            });

            return await GetUser(id);
        }

        [HttpPost("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            _auditService.Log("ToggleStatus", "User", user.Id, User, new { user.IsActive });

            return Ok();
        }

        [HttpGet("coaches")]
        [Authorize(Roles = "Admin,Coach")]
        public async Task<ActionResult<List<UserListDto>>> GetCoaches()
        {
            var coaches = await _userManager.GetUsersInRoleAsync("Coach");

            var coachIds = coaches.Select(c => c.Id).ToList();
            List<CoachTeam> allCoachTeams = [];
            if (await CoachTeamsTableExistsAsync())
            {
                allCoachTeams = await _context.CoachTeams
                    .Include(ct => ct.Team)
                    .Where(ct => coachIds.Contains(ct.CoachId))
                    .ToListAsync();
            }

            var result = coaches.Select(u =>
            {
                var userTeams = allCoachTeams
                    .Where(ct => ct.CoachId == u.Id)
                    .Select(ct => new UserTeamDto(ct.TeamId, ct.Team.Name, ct.IsPrimary))
                    .ToList();

                return new UserListDto(
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email ?? string.Empty,
                    u.PhoneNumber,
                    "Coach",
                    u.IsActive,
                    u.CreatedAt,
                    userTeams.OrderByDescending(t => t.IsPrimary).Select(t => t.TeamName).FirstOrDefault(),
                    userTeams
                );
            }).ToList();

            return Ok(result);
        }

        [HttpPost("{userId}/teams/{teamId}")]
        public async Task<IActionResult> AssignTeam(string userId, Guid teamId, [FromQuery] bool isPrimary = false)
        {
            bool exists;
            try
            {
                exists = await _context.CoachTeams
                    .AnyAsync(ct => ct.CoachId == userId && ct.TeamId == teamId);
            }
            catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(ex, "CoachTeams table is missing in database while assigning team.");
                return StatusCode(500, "Databáza nie je synchronizovaná. Aplikujte migrácie.");
            }

            if (exists)
                return BadRequest("Tím je už priradený");

            if (isPrimary)
            {
                List<CoachTeam> existing;
                try
                {
                    existing = await _context.CoachTeams
                        .Where(ct => ct.CoachId == userId && ct.IsPrimary)
                        .ToListAsync();
                }
                catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError(ex, "CoachTeams table is missing in database while setting primary team.");
                    return StatusCode(500, "Databáza nie je synchronizovaná. Aplikujte migrácie.");
                }
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
            return Ok();
        }

        [HttpDelete("{userId}/teams/{teamId}")]
        public async Task<IActionResult> RemoveTeam(string userId, Guid teamId)
        {
            CoachTeam? coachTeam;
            try
            {
                coachTeam = await _context.CoachTeams
                    .FirstOrDefaultAsync(ct => ct.CoachId == userId && ct.TeamId == teamId);
            }
            catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(ex, "CoachTeams table is missing in database while removing team assignment.");
                return StatusCode(500, "Databáza nie je synchronizovaná. Aplikujte migrácie.");
            }

            if (coachTeam == null)
                return NotFound();

            _context.CoachTeams.Remove(coachTeam);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            List<CoachTeam> coachLinks;
            try
            {
                coachLinks = await _context.CoachTeams.Where(ct => ct.CoachId == id).ToListAsync();
            }
            catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex, "CoachTeams table is missing in database while deleting user {UserId}. Continuing without coach-team cleanup.", id);
                coachLinks = [];
            }
            if (coachLinks.Count > 0)
            {
                _context.CoachTeams.RemoveRange(coachLinks);
            }

            var announcements = await _context.Announcements
                .Include(a => a.Attachments)
                .Where(a => a.AuthorUserId == id)
                .ToListAsync();

            foreach (var announcement in announcements)
            {
                _context.AnnouncementAttachments.RemoveRange(announcement.Attachments);
            }

            if (announcements.Count > 0)
            {
                _context.Announcements.RemoveRange(announcements);
            }

            var sentAbsences = await _context.AbsenceRequests
                .Where(a => a.ParentId == id)
                .ToListAsync();

            if (sentAbsences.Count > 0)
            {
                _context.AbsenceRequests.RemoveRange(sentAbsences);
            }

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
                return BadRequest(result.Errors);

            _auditService.Log("Delete", "User", id, User, null);
            return Ok();
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
                var distinctParentIds = parentIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList();

                var existingLinks = await _context.ParentChildren
                    .Where(pc => pc.ChildId == childProfile.Id)
                    .ToListAsync();

                _context.ParentChildren.RemoveRange(existingLinks);

                for (int i = 0; i < distinctParentIds.Count; i++)
                {
                    var parentId = distinctParentIds[i];
                    _context.ParentChildren.Add(new ParentChild
                    {
                        ParentId = parentId,
                        ChildId = childProfile.Id,
                        IsPrimary = i == 0
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task<bool> CoachTeamsTableExistsAsync()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                var shouldClose = connection.State != ConnectionState.Open;

                if (shouldClose)
                    await connection.OpenAsync();

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'[dbo].[CoachTeams]', N'U') IS NULL THEN 0 ELSE 1 END";
                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result) == 1;
                }
                finally
                {
                    if (shouldClose)
                        await connection.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to verify CoachTeams table existence.");
                return false;
            }
        }
    }
}