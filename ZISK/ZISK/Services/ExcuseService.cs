using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Extensions;
using ZISK.Shared.DTOs.Excuses;
using ExcuseStatus = ZISK.Shared.Enums.ExcuseStatus;

namespace ZISK.Services;

public class ExcuseService : IExcuseService
{
    private readonly ApplicationDbContext _context;
    private readonly ITeamAccessService _teamAccessService;
    private readonly IAuditService _auditService;

    public ExcuseService(ApplicationDbContext context, ITeamAccessService teamAccessService, IAuditService auditService)
    {
        _context = context;
        _teamAccessService = teamAccessService;
        _auditService = auditService;
    }

    public async Task<List<ExcuseListDto>> GetExcusesAsync(ExcuseStatus? status, ClaimsPrincipal user)
    {
        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);

        IQueryable<AbsenceRequest> query;
        if (accessibleTeamIds is not null)
        {
            var allowedChildIds = await _context.TeamMembers
                .Where(tm => accessibleTeamIds.Contains(tm.TeamId))
                .Select(tm => tm.UserId)
                .ToListAsync();

            query = _context.AbsenceRequests
                .Include(ar => ar.Child)
                .Where(ar => allowedChildIds.Contains(ar.ChildId))
                .AsNoTracking();
        }
        else
        {
            query = _context.AbsenceRequests
                .Include(ar => ar.Child)
                .AsNoTracking();
        }

        if (status.HasValue)
        {
            var dbStatus = (AbsenceRequestStatus)(int)status.Value;
            query = query.Where(ar => ar.Status == dbStatus);
        }

        var items = await query.OrderByDescending(ar => ar.CreatedAt).ToListAsync();

        var childIds = items.Select(i => i.ChildId).Distinct().ToList();
        var teamMap = await BuildChildTeamMapAsync(childIds);

        return items.Select(ar => new ExcuseListDto(
            ar.Id,
            ar.TrainingEventId,
            $"{ar.Child.FirstName} {ar.Child.LastName}",
            teamMap.TryGetValue(ar.ChildId, out var tn) ? tn : "Bez tímu",
            ar.DateFrom,
            ar.DateTo,
            ar.Reason,
            (ExcuseStatus)(int)ar.Status,
            ar.CreatedAt
        )).ToList();
    }

    public async Task<ExcuseDto> GetExcuseAsync(Guid id, ClaimsPrincipal user)
    {
        var excuse = await _context.AbsenceRequests
            .Include(ar => ar.Child)
            .Include(ar => ar.TrainingEvent)
            .Include(ar => ar.Parent)
            .AsNoTracking()
            .FirstOrDefaultAsync(ar => ar.Id == id)
            ?? throw new KeyNotFoundException();

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null)
        {
            var childTeamIds = await _context.TeamMembers
                .Where(tm => tm.UserId == excuse.ChildId)
                .Select(tm => tm.TeamId)
                .ToListAsync();
            if (!childTeamIds.Any(tId => accessibleTeamIds.Contains(tId)))
                throw new UnauthorizedAccessException();
        }

        var teamMap = await BuildChildTeamMapAsync([excuse.ChildId]);

        return new ExcuseDto(
            excuse.Id,
            excuse.ChildId,
            $"{excuse.Child.FirstName} {excuse.Child.LastName}",
            excuse.TrainingEventId,
            excuse.TrainingEvent?.Title,
            excuse.DateFrom,
            excuse.DateTo,
            excuse.Reason,
            excuse.Note,
            (ExcuseStatus)(int)excuse.Status,
            excuse.CreatedAt,
            teamMap.TryGetValue(excuse.ChildId, out var tn) ? tn : "Bez tímu"
        );
    }

    public async Task<ExcuseDto> CreateExcuseAsync(CreateExcuseRequest request, ClaimsPrincipal user)
    {
        var userId = user.GetRequiredUserId();

        if (user.IsInRole("Child"))
            throw new UnauthorizedAccessException();

        ValidateExcuseFields(request.Reason, request.Note, request.DateFrom, request.DateTo);

        if (request.TrainingEventId == null && request.DateFrom == null)
            throw new ArgumentException("Musíte zadať buď konkrétny tréning alebo dátum absencie");

        var child = await _context.Users.FindAsync(request.ChildId)
            ?? throw new ArgumentException("Dieťa neexistuje");

        if (user.IsInRole("Parent"))
        {
            var ownsChild = await _context.ParentChildren
                .AnyAsync(pc => pc.ParentId == userId && pc.ChildId == request.ChildId);
            if (!ownsChild)
                throw new UnauthorizedAccessException();
        }
        else if (user.IsInRole("Athlete"))
        {
            if (request.ChildId != userId)
                throw new UnauthorizedAccessException();
        }

        var absence = new AbsenceRequest
        {
            Id = Guid.NewGuid(),
            ChildId = request.ChildId,
            ParentId = userId,
            TrainingEventId = request.TrainingEventId,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            Note = request.Note,
            Status = AbsenceRequestStatus.Received,
            CreatedAt = DateTime.UtcNow
        };

        _context.AbsenceRequests.Add(absence);
        await _context.SaveChangesAsync();
        _auditService.Log("Create", "Excuse", absence.Id.ToString(), user, new { absence.ChildId, absence.DateFrom, absence.DateTo });

        var teamMap = await BuildChildTeamMapAsync([request.ChildId]);

        return new ExcuseDto(
            absence.Id, absence.ChildId, $"{child.FirstName} {child.LastName}",
            absence.TrainingEventId, null, absence.DateFrom, absence.DateTo,
            absence.Reason, absence.Note, ExcuseStatus.Received, absence.CreatedAt,
            teamMap.TryGetValue(request.ChildId, out var tn) ? tn : "Bez tímu"
        );
    }

    public async Task UpdateStatusAsync(Guid id, UpdateExcuseStatusRequest request, ClaimsPrincipal user)
    {
        var userId = user.GetUserId();
        var excuse = await _context.AbsenceRequests.FindAsync(id)
            ?? throw new KeyNotFoundException();

        excuse.ReviewNote = request.ReviewNote;
        excuse.ReviewedByUserId = userId;
        excuse.ProcessedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _auditService.Log("Review", "Excuse", excuse.Id.ToString(), user, new { excuse.ReviewedByUserId, excuse.ProcessedAt });
    }

    public async Task UpdateExcuseAsync(Guid id, UpdateExcuseRequest request, ClaimsPrincipal user)
    {
        ValidateExcuseFields(request.Reason, request.Note, null, request.DateTo, request.DateFrom);

        var userId = user.GetUserId();
        var excuse = await _context.AbsenceRequests.FindAsync(id)
            ?? throw new KeyNotFoundException();

        if (user.IsInRole("Child"))
            throw new UnauthorizedAccessException();

        if (!user.IsInRole("Admin") && excuse.ParentId != userId)
        {
            if (!user.IsInRole("Athlete") || excuse.ChildId != userId)
                throw new UnauthorizedAccessException();
        }

        excuse.DateFrom = request.DateFrom;
        excuse.DateTo = request.DateTo;
        excuse.Reason = request.Reason;
        excuse.Note = request.Note;

        await _context.SaveChangesAsync();
        _auditService.Log("Update", "Excuse", excuse.Id.ToString(), user, new { excuse.DateFrom, excuse.DateTo });
    }

    public async Task DeleteExcuseAsync(Guid id, ClaimsPrincipal user)
    {
        var userId = user.GetUserId();
        var excuse = await _context.AbsenceRequests.FindAsync(id)
            ?? throw new KeyNotFoundException();

        if (user.IsInRole("Child"))
            throw new UnauthorizedAccessException();

        if (!user.IsInRole("Admin") && excuse.ParentId != userId)
        {
            if (!user.IsInRole("Athlete") || excuse.ChildId != userId)
                throw new UnauthorizedAccessException();
        }

        _context.AbsenceRequests.Remove(excuse);
        await _context.SaveChangesAsync();
        _auditService.Log("Delete", "Excuse", excuse.Id.ToString(), user);
    }

    public async Task<List<ExcuseListDto>> GetMyExcusesAsync(ClaimsPrincipal user)
    {
        var userId = user.GetRequiredUserId();

        IQueryable<AbsenceRequest> query;

        if (user.IsInRole("Parent"))
        {
            query = _context.AbsenceRequests
                .Include(ar => ar.Child)
                .Where(ar => ar.ParentId == userId);
        }
        else if (user.IsInRole("Athlete") || user.IsInRole("Child"))
        {
            query = _context.AbsenceRequests
                .Include(ar => ar.Child)
                .Where(ar => ar.ChildId == userId);
        }
        else
        {
            throw new UnauthorizedAccessException();
        }

        var items = await query.OrderByDescending(ar => ar.CreatedAt).AsNoTracking().ToListAsync();
        var childIds = items.Select(i => i.ChildId).Distinct().ToList();
        var teamMap = await BuildChildTeamMapAsync(childIds);

        return items.Select(ar => new ExcuseListDto(
            ar.Id,
            ar.TrainingEventId,
            $"{ar.Child.FirstName} {ar.Child.LastName}",
            teamMap.TryGetValue(ar.ChildId, out var tn) ? tn : "Bez tímu",
            ar.DateFrom,
            ar.DateTo,
            ar.Reason,
            (ExcuseStatus)(int)ar.Status,
            ar.CreatedAt
        )).ToList();
    }

    public async Task<int> GetPendingCountAsync(ClaimsPrincipal user)
    {
        var userId = user.GetRequiredUserId();

        if (user.IsInRole("Admin") || user.IsInRole("Coach"))
        {
            var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
            if (accessibleTeamIds is null)
                return await _context.AbsenceRequests.CountAsync(ar => ar.Status == AbsenceRequestStatus.Received);

            var allowedChildIds = await _context.TeamMembers
                .Where(tm => accessibleTeamIds.Contains(tm.TeamId))
                .Select(tm => tm.UserId)
                .ToListAsync();

            return await _context.AbsenceRequests.CountAsync(ar =>
                ar.Status == AbsenceRequestStatus.Received && allowedChildIds.Contains(ar.ChildId));
        }

        if (user.IsInRole("Parent"))
            return await _context.AbsenceRequests.CountAsync(ar =>
                ar.Status == AbsenceRequestStatus.Received && ar.ParentId == userId);

        if (user.IsInRole("Athlete") || user.IsInRole("Child"))
            return await _context.AbsenceRequests.CountAsync(ar =>
                ar.Status == AbsenceRequestStatus.Received && ar.ChildId == userId);

        return 0;
    }

    private async Task<Dictionary<string, string>> BuildChildTeamMapAsync(List<string> childIds)
    {
        var memberships = await _context.TeamMembers
            .Include(tm => tm.Team)
            .Where(tm => childIds.Contains(tm.UserId))
            .ToListAsync();

        return memberships
            .GroupBy(tm => tm.UserId)
            .ToDictionary(g => g.Key, g => g.First().Team.Name);
    }

    private static void ValidateExcuseFields(string? reason, string? note, DateTime? dateFrom, DateTime? dateTo, DateTime? dateFromForRange = null)
    {
        if (!string.IsNullOrWhiteSpace(reason) && (reason.Length < 3 || reason.Length > 200))
            throw new ArgumentException("Dôvod musí mať 3-200 znakov");

        if (note != null && note.Length > 500)
            throw new ArgumentException("Poznámka môže mať max 500 znakov");

        if (dateFrom.HasValue && dateFrom.Value < DateTime.UtcNow.AddDays(-30))
            throw new ArgumentException("Dátum nemôže byť starší ako 30 dní");

        var effectiveDateFrom = dateFromForRange ?? dateFrom;
        if (dateTo.HasValue && effectiveDateFrom.HasValue && dateTo.Value < effectiveDateFrom.Value)
            throw new ArgumentException("Dátum 'Do' nemôže byť pred dátumom 'Od'");
    }
}
