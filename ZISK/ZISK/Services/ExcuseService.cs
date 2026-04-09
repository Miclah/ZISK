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
        var query = _context.AbsenceRequests
            .Include(ar => ar.Child).ThenInclude(c => c.Team)
            .AsNoTracking();

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null)
            query = query.Where(ar => ar.Child.TeamId.HasValue && accessibleTeamIds.Contains(ar.Child.TeamId.Value));

        if (status.HasValue)
        {
            var dbStatus = (AbsenceRequestStatus)(int)status.Value;
            query = query.Where(ar => ar.Status == dbStatus);
        }

        return await query
            .OrderByDescending(ar => ar.CreatedAt)
            .Select(ar => new ExcuseListDto(
                ar.Id,
                ar.TrainingEventId,
                $"{ar.Child.FirstName} {ar.Child.LastName}",
                ar.Child.Team != null ? ar.Child.Team.Name : "Bez tímu",
                ar.DateFrom,
                ar.DateTo,
                ar.Reason,
                (ExcuseStatus)(int)ar.Status,
                ar.CreatedAt
            ))
            .ToListAsync();
    }

    public async Task<ExcuseDto> GetExcuseAsync(Guid id, ClaimsPrincipal user)
    {
        var excuse = await _context.AbsenceRequests
            .Include(ar => ar.Child).ThenInclude(c => c.Team)
            .Include(ar => ar.TrainingEvent)
            .Include(ar => ar.Parent)
            .AsNoTracking()
            .FirstOrDefaultAsync(ar => ar.Id == id)
            ?? throw new KeyNotFoundException();

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && (!excuse.Child.TeamId.HasValue || !accessibleTeamIds.Contains(excuse.Child.TeamId.Value)))
            throw new UnauthorizedAccessException();

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
            excuse.Child.Team?.Name ?? "Bez tímu"
        );
    }

    public async Task<ExcuseDto> CreateExcuseAsync(CreateExcuseRequest request, ClaimsPrincipal user)
    {
        var userId = user.GetRequiredUserId();

        if (user.IsInRole("Child"))
            throw new UnauthorizedAccessException();

        var userEmail = user.GetEmail();

        ValidateExcuseFields(request.Reason, request.Note, request.DateFrom, request.DateTo);

        if (request.TrainingEventId == null && request.DateFrom == null)
            throw new ArgumentException("Musíte zadať buď konkrétny tréning alebo dátum absencie");

        var child = await _context.ChildProfiles
            .Include(c => c.Team)
            .FirstOrDefaultAsync(c => c.Id == request.ChildId)
            ?? throw new ArgumentException("Dieťa neexistuje");

        if (user.IsInRole("Parent"))
        {
            var ownsChild = await _context.ParentChildren.AnyAsync(pc => pc.ParentId == userId && pc.ChildId == request.ChildId);
            if (!ownsChild)
                throw new UnauthorizedAccessException();
        }
        else if (user.IsInRole("Athlete"))
        {
            if (string.IsNullOrWhiteSpace(userEmail) || !string.Equals(child.Email, userEmail, StringComparison.OrdinalIgnoreCase))
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

        return new ExcuseDto(
            absence.Id, absence.ChildId, $"{child.FirstName} {child.LastName}",
            absence.TrainingEventId, null, absence.DateFrom, absence.DateTo,
            absence.Reason, absence.Note, ExcuseStatus.Received, absence.CreatedAt,
            child.Team?.Name ?? "Bez tímu"
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
        var userEmail = user.GetEmail();
        var excuse = await _context.AbsenceRequests.FindAsync(id)
            ?? throw new KeyNotFoundException();

        if (user.IsInRole("Child"))
            throw new UnauthorizedAccessException();

        if (!user.IsInRole("Admin") && excuse.ParentId != userId)
        {
            if (!user.IsInRole("Athlete") || string.IsNullOrWhiteSpace(userEmail))
                throw new UnauthorizedAccessException();

            var isOwnProfile = await _context.ChildProfiles.AnyAsync(c => c.Id == excuse.ChildId && c.Email == userEmail);
            if (!isOwnProfile)
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
        var userEmail = user.GetEmail();
        var excuse = await _context.AbsenceRequests.FindAsync(id)
            ?? throw new KeyNotFoundException();

        if (user.IsInRole("Child"))
            throw new UnauthorizedAccessException();

        if (!user.IsInRole("Admin") && excuse.ParentId != userId)
        {
            if (!user.IsInRole("Athlete") || string.IsNullOrWhiteSpace(userEmail))
                throw new UnauthorizedAccessException();

            var isOwnProfile = await _context.ChildProfiles.AnyAsync(c => c.Id == excuse.ChildId && c.Email == userEmail);
            if (!isOwnProfile)
                throw new UnauthorizedAccessException();
        }

        _context.AbsenceRequests.Remove(excuse);
        await _context.SaveChangesAsync();
        _auditService.Log("Delete", "Excuse", excuse.Id.ToString(), user);
    }

    public async Task<List<ExcuseListDto>> GetMyExcusesAsync(ClaimsPrincipal user)
    {
        var userId = user.GetRequiredUserId();
        var userEmail = user.GetEmail();

        var query = _context.AbsenceRequests
            .Include(ar => ar.Child).ThenInclude(c => c.Team)
            .AsQueryable();

        if (user.IsInRole("Parent"))
        {
            query = query.Where(ar => ar.ParentId == userId);
        }
        else if (user.IsInRole("Athlete") || user.IsInRole("Child"))
        {
            if (string.IsNullOrWhiteSpace(userEmail))
                return [];
            query = query.Where(ar => ar.Child.Email == userEmail);
        }
        else
        {
            throw new UnauthorizedAccessException();
        }

        return await query
            .OrderByDescending(ar => ar.CreatedAt)
            .Select(ar => new ExcuseListDto(
                ar.Id,
                ar.TrainingEventId,
                $"{ar.Child.FirstName} {ar.Child.LastName}",
                ar.Child.Team != null ? ar.Child.Team.Name : "Bez tímu",
                ar.DateFrom,
                ar.DateTo,
                ar.Reason,
                (ExcuseStatus)(int)ar.Status,
                ar.CreatedAt
            ))
            .ToListAsync();
    }

    public async Task<int> GetPendingCountAsync(ClaimsPrincipal user)
    {
        var userId = user.GetRequiredUserId();
        var userEmail = user.GetEmail();

        if (user.IsInRole("Admin") || user.IsInRole("Coach"))
        {
            var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
            if (accessibleTeamIds is null)
                return await _context.AbsenceRequests.CountAsync(ar => ar.Status == AbsenceRequestStatus.Received);

            return await _context.AbsenceRequests.CountAsync(ar =>
                ar.Status == AbsenceRequestStatus.Received &&
                ar.Child.TeamId.HasValue && accessibleTeamIds.Contains(ar.Child.TeamId.Value));
        }

        if (user.IsInRole("Parent"))
            return await _context.AbsenceRequests.CountAsync(ar =>
                ar.Status == AbsenceRequestStatus.Received && ar.ParentId == userId);

        if ((user.IsInRole("Athlete") || user.IsInRole("Child")) && !string.IsNullOrWhiteSpace(userEmail))
            return await _context.AbsenceRequests.CountAsync(ar =>
                ar.Status == AbsenceRequestStatus.Received && ar.Child.Email == userEmail);

        return 0;
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
