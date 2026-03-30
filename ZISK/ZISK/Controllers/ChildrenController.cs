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

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var result = new List<ChildDto>();

        if (User.IsInRole("Athlete") || User.IsInRole("Child"))
        {
            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                var ownProfiles = await _context.ChildProfiles
                    .Include(c => c.Team)
                    .Where(c => c.IsActive && c.Email == userEmail)
                    .Select(c => new ChildDto(
                        c.Id,
                        c.FirstName,
                        c.LastName,
                        c.TeamId,
                        c.Team != null ? c.Team.Name : null,
                        true
                    ))
                    .ToListAsync();

                result.AddRange(ownProfiles);
            }
        }

        if (User.IsInRole("Parent"))
        {
            var children = await _context.ParentChildren
                .Include(pc => pc.Child)
                    .ThenInclude(c => c.Team)
                .Where(pc => pc.ParentId == userId)
                .Select(pc => new ChildDto(
                    pc.Child.Id,
                    pc.Child.FirstName,
                    pc.Child.LastName,
                    pc.Child.TeamId,
                    pc.Child.Team != null ? pc.Child.Team.Name : null,
                    false
                ))
                .ToListAsync();

            result.AddRange(children);
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
        var children = await _context.ChildProfiles
            .Include(c => c.Team)
            .Where(c => c.IsActive)
            .Select(c => new ChildDto(
                c.Id,
                c.FirstName,
                c.LastName,
                c.TeamId,
                c.Team != null ? c.Team.Name : null,
                false
            ))
            .ToListAsync();

        return Ok(children);
    }
}

public record ChildDto(Guid Id, string FirstName, string LastName, Guid? TeamId, string? TeamName, bool IsOwnProfile);
