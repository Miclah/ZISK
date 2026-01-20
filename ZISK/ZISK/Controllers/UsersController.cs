using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
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

        public UsersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<ActionResult<List<UserListDto>>> GetUsers([FromQuery] string? role = null)
        {
            var users = await _userManager.Users.ToListAsync();
            var result = new List<UserListDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var userRole = roles.FirstOrDefault() ?? "Parent";

                if (role != null && userRole != role)
                    continue;

                result.Add(new UserListDto(
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Email ?? "",
                    userRole,
                    user.IsActive
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
            var teams = await _context.CoachTeams
                .Where(ct => ct.CoachId == id)
                .Include(ct => ct.Team)
                .Select(ct => new UserTeamDto(ct.TeamId, ct.Team.Name, ct.IsPrimary))
                .ToListAsync();

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
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors.First().Description);

            await _userManager.AddToRoleAsync(user, request.Role);

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

            await _userManager.UpdateAsync(user);

            if (request.Role != null)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, request.Role);
            }

            if (request.TeamIds != null)
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
            }

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

            return Ok();
        }

        [HttpGet("coaches")]
        [Authorize(Roles = "Admin,Coach")]
        public async Task<ActionResult<List<UserListDto>>> GetCoaches()
        {
            var coaches = await _userManager.GetUsersInRoleAsync("Coach");
            return Ok(coaches.Select(u => new UserListDto(
                u.Id, u.FirstName, u.LastName, u.Email ?? "", "Coach", u.IsActive
            )).ToList());
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
    }
}