using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
            var users = await _userManager.Users.ToListAsync();
            var result = new List<UserListDto>();

            var allCoachTeams = await _context.CoachTeams
                .Include(ct => ct.Team)
                .ToListAsync();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var userRole = roles.FirstOrDefault() ?? "Parent";

                if (role != null && userRole != role)
                    continue;

                var userTeams = allCoachTeams
                    .Where(ct => ct.CoachId == user.Id)
                    .Select(ct => new UserTeamDto(ct.TeamId, ct.Team.Name, ct.IsPrimary))
                    .ToList();

                result.Add(new UserListDto(
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Email ?? "",
                    userRole,
                    user.IsActive,
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

            return Ok(new UserDto(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email ?? "",
                roles.FirstOrDefault() ?? "Parent",
                user.IsActive,
                user.CreatedAt,
                teams
            ));
        }

        [HttpPost]
        public async Task<ActionResult<UserDto>> CreateUser(CreateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Length < 2 || request.FirstName.Length > 100)
                return BadRequest("Meno musí mať 2-100 znakov");

            if (string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Length < 2 || request.LastName.Length > 100)
                return BadRequest("Priezvisko musí mať 2-100 znakov");

            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains("@"))
                return BadRequest("Neplatný email");

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 4)
                return BadRequest("Heslo musí mať minimálne 4 znaky");

            var validRoles = new[] { "Admin", "Coach", "Parent", "Athlete", "Child" };
            if (!validRoles.Contains(request.Role))
                return BadRequest("Neplatná rola");

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                RodneCislo = request.RodneCislo,
                DateOfBirth = request.DateOfBirth.HasValue ? DateOnly.FromDateTime(request.DateOfBirth.Value) : null,
                Bydlisko = request.Bydlisko,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors.First().Description);

            await _userManager.AddToRoleAsync(user, request.Role);
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
            if (request.DateOfBirth.HasValue)
                user.DateOfBirth = DateOnly.FromDateTime(request.DateOfBirth.Value);
            if (request.Bydlisko != null)
                user.Bydlisko = request.Bydlisko;

            await _userManager.UpdateAsync(user);

            if (request.Role != null)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, request.Role);
                _auditService.Log("RoleChanged", "User", user.Id, User, new { From = currentRoles.FirstOrDefault(), To = request.Role });
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
                    return StatusCode(500, "Databáza nie je synchronizovaná. Reštartujte aplikáciu a apliklite migrácie.");
                }
            }

            _auditService.Log("Update", "User", user.Id, User, new { request.FirstName, request.LastName, request.IsActive });

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
            var allCoachTeams = await _context.CoachTeams
                .Include(ct => ct.Team)
                .Where(ct => coachIds.Contains(ct.CoachId))
                .ToListAsync();

            var result = coaches.Select(u => {
                var userTeams = allCoachTeams
                    .Where(ct => ct.CoachId == u.Id)
                    .Select(ct => new UserTeamDto(ct.TeamId, ct.Team.Name, ct.IsPrimary))
                    .ToList();

                return new UserListDto(
                    u.Id, u.FirstName, u.LastName, u.Email ?? "", "Coach", u.IsActive, userTeams
                );
            }).ToList();

            return Ok(result);
        }

        [HttpPost("{userId}/teams/{teamId}")]
        public async Task<IActionResult> AssignTeam(string userId, Guid teamId, [FromQuery] bool isPrimary = false)
        {
            var exists = await _context.CoachTeams
                .AnyAsync(ct => ct.CoachId == userId && ct.TeamId == teamId);

            if (exists)
                return BadRequest("Tím je už priradený");

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
            return Ok();
        }

        [HttpDelete("{userId}/teams/{teamId}")]
        public async Task<IActionResult> RemoveTeam(string userId, Guid teamId)
        {
            var coachTeam = await _context.CoachTeams
                .FirstOrDefaultAsync(ct => ct.CoachId == userId && ct.TeamId == teamId);

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

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            _auditService.Log("Delete", "User", id, User, null);

            return Ok();
        }
    }
}