using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;
using ZISK.Shared.DTOs.Teams;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TeamsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITeamAccessService _teamAccessService;
    private readonly IAuditService _auditService;

    public TeamsController(ApplicationDbContext context, ITeamAccessService teamAccessService, IAuditService auditService)
    {
        _context = context;
        _teamAccessService = teamAccessService;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TeamDto>>> GetTeams([FromQuery] bool? activeOnly = true)
    {
        var query = _context.Teams
            .Include(t => t.Members)
            .AsNoTracking();

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(User);
        if (accessibleTeamIds is not null)
        {
            query = query.Where(t => accessibleTeamIds.Contains(t.Id));
        }

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

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(User);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(team.Id))
            return Forbid();

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
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 2 || request.Name.Length > 100)
            return BadRequest("Názov musí mať 2-100 znakov");

        if (request.ShortName != null && request.ShortName.Length > 10)
            return BadRequest("Skratka môže mať max 10 znakov");

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
        _auditService.Log("Create", "Team", team.Id.ToString(), User, new { team.Name, team.ShortName });

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
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 2 || request.Name.Length > 100)
            return BadRequest("Názov musí mať 2-100 znakov");

        if (request.ShortName != null && request.ShortName.Length > 10)
            return BadRequest("Skratka môže mať max 10 znakov");

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
        _auditService.Log("Update", "Team", team.Id.ToString(), User, new { team.Name, team.IsActive });

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
        _auditService.Log("Delete", "Team", team.Id.ToString(), User, new { team.Name });

        return NoContent();
    }

    [HttpPost("{id:guid}/members/{childId:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> AddMember(Guid id, Guid childId)
    {
        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(User);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(id))
            return Forbid();

        var team = await _context.Teams.FindAsync(id);
        if (team == null)
            return NotFound("Tím neexistuje");

        var child = await _context.ChildProfiles.FindAsync(childId);
        if (child == null)
            return NotFound("Člen neexistuje");

        child.TeamId = id;
        await _context.SaveChangesAsync();
        _auditService.Log("AssignMember", "Team", id.ToString(), User, new { ChildId = childId });

        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{childId:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid childId)
    {
        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(User);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(id))
            return Forbid();

        var child = await _context.ChildProfiles.FindAsync(childId);
        if (child == null)
            return NotFound("Člen neexistuje");

        if (child.TeamId != id)
            return BadRequest("Člen nie je v tomto tíme");

        child.TeamId = null;
        await _context.SaveChangesAsync();
        _auditService.Log("RemoveMember", "Team", id.ToString(), User, new { ChildId = childId });

        return NoContent();
    }
}
