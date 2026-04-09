using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Teams;

namespace ZISK.Services;

public class TeamService : ITeamService
{
    private readonly ApplicationDbContext _context;
    private readonly ITeamAccessService _teamAccessService;
    private readonly IAuditService _auditService;

    public TeamService(ApplicationDbContext context, ITeamAccessService teamAccessService, IAuditService auditService)
    {
        _context = context;
        _teamAccessService = teamAccessService;
        _auditService = auditService;
    }

    public async Task<List<TeamDto>> GetTeamsAsync(bool? activeOnly, ClaimsPrincipal user)
    {
        var query = _context.Teams.Include(t => t.Members).AsNoTracking();

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null)
            query = query.Where(t => accessibleTeamIds.Contains(t.Id));

        if (activeOnly == true)
            query = query.Where(t => t.IsActive);

        return await query
            .OrderBy(t => t.Name)
            .Select(t => new TeamDto(t.Id, t.Name, t.ShortName, t.Description, t.IsActive, t.Members.Count(m => m.IsActive)))
            .ToListAsync();
    }

    public async Task<TeamDetailDto> GetTeamAsync(Guid id, ClaimsPrincipal user)
    {
        var team = await _context.Teams
            .Include(t => t.Members.Where(m => m.IsActive))
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException();

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(team.Id))
            throw new UnauthorizedAccessException();

        var memberIds = team.Members.Select(m => m.Id).ToList();
        var parentLinks = await _context.ParentChildren
            .Include(pc => pc.Parent)
            .Where(pc => memberIds.Contains(pc.ChildId))
            .AsNoTracking()
            .ToListAsync();

        return new TeamDetailDto(
            team.Id, team.Name, team.ShortName, team.Description, team.IsActive, team.CreatedAt,
            team.Members.Select(m => new TeamMemberDto(
                m.Id, m.FirstName, m.LastName, m.Email, m.DateOfBirth,
                parentLinks
                    .Where(p => p.ChildId == m.Id)
                    .Select(p => $"{p.Parent.FirstName} {p.Parent.LastName} ({(string.IsNullOrWhiteSpace(p.Parent.PhoneNumber) ? "bez telefónu" : p.Parent.PhoneNumber)})")
                    .Distinct().ToList()
            )).OrderBy(m => m.LastName).ToList()
        );
    }

    public async Task<TeamDto> CreateTeamAsync(CreateTeamRequest request, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 2 || request.Name.Length > 100)
            throw new ArgumentException("Názov musí mať 2-100 znakov");

        if (request.ShortName != null && request.ShortName.Length > 10)
            throw new ArgumentException("Skratka môže mať max 10 znakov");

        await EnsureUniqueNameAsync(request.Name);

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
        _auditService.Log("Create", "Team", team.Id.ToString(), user, new { team.Name, team.ShortName });

        return new TeamDto(team.Id, team.Name, team.ShortName, team.Description, team.IsActive, 0);
    }

    public async Task UpdateTeamAsync(Guid id, UpdateTeamRequest request, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 2 || request.Name.Length > 100)
            throw new ArgumentException("Názov musí mať 2-100 znakov");

        if (request.ShortName != null && request.ShortName.Length > 10)
            throw new ArgumentException("Skratka môže mať max 10 znakov");

        var team = await _context.Teams.FindAsync(id) ?? throw new KeyNotFoundException();

        await EnsureUniqueNameAsync(request.Name, excludeId: id);

        team.Name = request.Name;
        team.ShortName = request.ShortName;
        team.Description = request.Description;
        team.IsActive = request.IsActive;

        await _context.SaveChangesAsync();
        _auditService.Log("Update", "Team", team.Id.ToString(), user, new { team.Name, team.IsActive });
    }

    public async Task DeleteTeamAsync(Guid id, ClaimsPrincipal user)
    {
        var team = await _context.Teams.Include(t => t.Members).FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException();

        if (team.Members.Any())
            throw new InvalidOperationException("Nemožno vymazať tím s členmi. Najprv presuňte členov do iného tímu.");

        _context.Teams.Remove(team);
        await _context.SaveChangesAsync();
        _auditService.Log("Delete", "Team", team.Id.ToString(), user, new { team.Name });
    }

    public async Task AddMemberAsync(Guid teamId, Guid childId, ClaimsPrincipal user)
    {
        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(teamId))
            throw new UnauthorizedAccessException();

        var team = await _context.Teams.FindAsync(teamId) ?? throw new KeyNotFoundException("Tím neexistuje");
        var child = await _context.ChildProfiles.FindAsync(childId) ?? throw new KeyNotFoundException("Člen neexistuje");

        child.TeamId = teamId;
        await _context.SaveChangesAsync();
        _auditService.Log("AssignMember", "Team", teamId.ToString(), user, new { ChildId = childId });
    }

    public async Task RemoveMemberAsync(Guid teamId, Guid childId, ClaimsPrincipal user)
    {
        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(teamId))
            throw new UnauthorizedAccessException();

        var child = await _context.ChildProfiles.FindAsync(childId) ?? throw new KeyNotFoundException("Člen neexistuje");

        if (child.TeamId != teamId)
            throw new ArgumentException("Člen nie je v tomto tíme");

        child.TeamId = null;
        await _context.SaveChangesAsync();
        _auditService.Log("RemoveMember", "Team", teamId.ToString(), user, new { ChildId = childId });
    }

    private async Task EnsureUniqueNameAsync(string name, Guid? excludeId = null)
    {
        var exists = excludeId.HasValue
            ? await _context.Teams.AnyAsync(t => t.Name == name && t.Id != excludeId.Value)
            : await _context.Teams.AnyAsync(t => t.Name == name);

        if (exists)
            throw new InvalidOperationException("Tím s týmto názvom už existuje");
    }
}
