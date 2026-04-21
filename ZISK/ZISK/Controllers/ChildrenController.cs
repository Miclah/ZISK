using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZISK.Data;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChildrenController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ChildrenController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("my")]
    public async Task<ActionResult<List<ChildDto>>> GetMyChildren()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = new List<ChildDto>();

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
                    true
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
                    false
                ));
            }
        }

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
        var childRoleId = await _context.Roles
            .Where(r => r.Name == "Child")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        if (childRoleId == null)
            return Ok(new List<ChildDto>());

        var childUserIds = await _context.UserRoles
            .Where(ur => ur.RoleId == childRoleId)
            .Select(ur => ur.UserId)
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
            return new ChildDto(u.Id, u.FirstName, u.LastName, m?.TeamId, m?.Team.Name, false);
        }).ToList();

        return Ok(result);
    }
}

public record ChildDto(string Id, string FirstName, string LastName, Guid? TeamId, string? TeamName, bool IsOwnProfile);
