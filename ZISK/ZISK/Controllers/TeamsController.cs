using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Teams;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TeamsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TeamsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<TeamDto>>> GetTeams([FromQuery] bool? activeOnly = true)
    {
        var query = _context.Teams
            .Include(t => t.Members)
            .AsNoTracking();

        if (activeOnly == true)
            query = query.Where(t => t.IsActive);

        var teams = await query
            .OrderBy(t => t.Name)
            .Select(t => new TeamDto(
                t.Id,
                t.Name,
                t.ShortName,
                t.Description,
                t.IsActive,
                t.Members.Count(m => m.IsActive)
            ))
            .ToListAsync();

        return Ok(teams);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TeamDetailDto>> GetTeam(Guid id)
    {
        var team = await _context.Teams
            .Include(t => t.Members.Where(m => m.IsActive))
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (team == null)
            return NotFound();

        return Ok(new TeamDetailDto(
            team.Id,
            team.Name,
            team.ShortName,
            team.Description,
            team.IsActive,
            team.CreatedAt,
            team.Members.Select(m => new TeamMemberDto(
                m.Id,
                m.FirstName,
                m.LastName,
                m.Email,
                m.DateOfBirth
            )).OrderBy(m => m.LastName).ToList()
        ));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TeamDto>> CreateTeam([FromBody] CreateTeamRequest request)
    {
        if (await _context.Teams.AnyAsync(t => t.Name == request.Name))
            return BadRequest("Tím s týmto názvom už existuje");

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ShortName = request.ShortName,
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTeam), new { id = team.Id }, new TeamDto(
            team.Id,
            team.Name,
            team.ShortName,
            team.Description,
            team.IsActive,
            0
        ));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateTeam(Guid id, [FromBody] UpdateTeamRequest request)
    {
        var team = await _context.Teams.FindAsync(id);
        if (team == null)
            return NotFound();

        if (await _context.Teams.AnyAsync(t => t.Name == request.Name && t.Id != id))
            return BadRequest("Tím s týmto názvom už existuje");

        team.Name = request.Name;
        team.ShortName = request.ShortName;
        team.Description = request.Description;
        team.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTeam(Guid id)
    {
        var team = await _context.Teams
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (team == null)
            return NotFound();

        if (team.Members.Any())
            return BadRequest("Nemožno vymazať tím s členmi. Najprv presuňte členov do iného tímu.");

        _context.Teams.Remove(team);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/members/{childId:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> AddMember(Guid id, Guid childId)
    {
        var team = await _context.Teams.FindAsync(id);
        if (team == null)
            return NotFound("Tím neexistuje");

        var child = await _context.ChildProfiles.FindAsync(childId);
        if (child == null)
            return NotFound("Člen neexistuje");

        child.TeamId = id;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{childId:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid childId)
    {
        var child = await _context.ChildProfiles.FindAsync(childId);
        if (child == null)
            return NotFound("Člen neexistuje");

        if (child.TeamId != id)
            return BadRequest("Člen nie je v tomto tíme");

        child.TeamId = null;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
