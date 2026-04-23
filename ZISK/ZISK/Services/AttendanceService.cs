using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Extensions;
using ZISK.Helpers;
using ZISK.Shared.DTOs.Attendance;
using AttendanceStatus = ZISK.Shared.Enums.AttendanceStatus;

namespace ZISK.Services;

public class AttendanceService : IAttendanceService
{
    private readonly ApplicationDbContext _context;
    private readonly ITeamAccessService _teamAccessService;
    private readonly IAuditService _auditService;

    public AttendanceService(ApplicationDbContext context, ITeamAccessService teamAccessService, IAuditService auditService)
    {
        _context = context;
        _teamAccessService = teamAccessService;
        _auditService = auditService;
    }

    public async Task<List<AttendanceRecordDto>> GetTrainingAttendanceAsync(Guid trainingEventId, ClaimsPrincipal user)
    {
        var training = await _context.TrainingEvents
            .Include(t => t.Team)
            .FirstOrDefaultAsync(t => t.Id == trainingEventId);

        if (training == null)
            throw new KeyNotFoundException("Tréning neexistuje");

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(training.TeamId))
            throw new UnauthorizedAccessException();

        await AutoCompleteForTrainingAsync(trainingEventId, false);

        return await _context.AttendanceRecords
            .Include(ar => ar.Child)
            .Where(ar => ar.TrainingEventId == trainingEventId)
            .OrderBy(ar => ar.Child.LastName)
            .Select(ar => new AttendanceRecordDto(
                ar.Id,
                ar.TrainingEventId,
                training.Title,
                training.StartTime,
                ar.ChildId,
                $"{ar.Child.FirstName} {ar.Child.LastName}",
                (AttendanceStatus)(int)ar.Status,
                ar.Note,
                ar.CoachComment,
                ar.RecordedAt
            ))
            .ToListAsync();
    }

    public async Task<List<UserAttendanceDto>> GetMyAttendanceAsync(ClaimsPrincipal user, DateTime? from, DateTime? to)
    {
        var userId = user.GetRequiredUserId();
        var childIds = new List<string>();

        if (user.IsInRole("Parent"))
        {
            childIds = await _context.ParentChildren
                .Where(pc => pc.ParentId == userId)
                .Select(pc => pc.ChildId)
                .ToListAsync();
        }
        else if (user.IsInRole("Athlete") || user.IsInRole("Child"))
        {
            childIds = [userId];
        }

        if (!childIds.Any())
            return [];

        var query = _context.AttendanceRecords
            .Include(ar => ar.TrainingEvent)
            .Where(ar => childIds.Contains(ar.ChildId));

        if (from.HasValue)
            query = query.Where(ar => ar.TrainingEvent.StartTime >= from.Value);

        if (to.HasValue)
            query = query.Where(ar => ar.TrainingEvent.StartTime <= to.Value);

        return await query
            .OrderByDescending(ar => ar.TrainingEvent.StartTime)
            .Select(ar => new UserAttendanceDto(
                ar.Id,
                ar.TrainingEventId,
                ar.TrainingEvent.Title,
                ar.TrainingEvent.StartTime,
                (AttendanceStatus)(int)ar.Status,
                ar.Note,
                ar.CoachComment
            ))
            .ToListAsync();
    }

    public async Task<AttendanceStatsDto> GetMemberStatsAsync(string childId, DateTime? from, DateTime? to)
    {
        var query = _context.AttendanceRecords
            .Include(ar => ar.TrainingEvent)
            .Where(ar => ar.ChildId == childId);

        if (from.HasValue)
            query = query.Where(ar => ar.TrainingEvent.StartTime >= from.Value);

        if (to.HasValue)
            query = query.Where(ar => ar.TrainingEvent.StartTime <= to.Value);

        var records = await query.ToListAsync();
        var counts = AttendanceCalculator.CountByStatus(records);

        return new AttendanceStatsDto(
            counts.Present,
            counts.Absent,
            counts.Excused,
            counts.Total,
            AttendanceCalculator.CalculatePercentage(counts.Present, counts.Total)
        );
    }

    public async Task<List<MemberAttendanceStatsDto>> GetTeamStatsAsync(Guid teamId, DateTime? from, DateTime? to)
    {
        var teamMemberIds = await _context.TeamMembers
            .Where(tm => tm.TeamId == teamId)
            .Select(tm => tm.UserId)
            .ToListAsync();

        var query = _context.AttendanceRecords
            .Include(ar => ar.TrainingEvent)
            .Include(ar => ar.Child)
            .Where(ar => teamMemberIds.Contains(ar.ChildId) && ar.Child.IsActive);

        if (from.HasValue)
            query = query.Where(ar => ar.TrainingEvent.StartTime >= from.Value);

        if (to.HasValue)
            query = query.Where(ar => ar.TrainingEvent.StartTime <= to.Value);

        var stats = await query
            .GroupBy(ar => new { ar.ChildId, ar.Child.FirstName, ar.Child.LastName })
            .Select(g => new MemberAttendanceStatsDto(
                g.Key.ChildId,
                $"{g.Key.FirstName} {g.Key.LastName}",
                g.Count(r => r.Status == Data.Entities.AttendanceStatus.Present),
                g.Count(r => r.Status == Data.Entities.AttendanceStatus.Absent),
                g.Count(r => r.Status == Data.Entities.AttendanceStatus.Excused),
                g.Count() > 0
                    ? Math.Round((double)g.Count(r => r.Status == Data.Entities.AttendanceStatus.Present) / g.Count() * 100, 1)
                    : 0
            ))
            .OrderByDescending(s => s.AttendancePercentage)
            .ToListAsync();

        var membersWithRecords = stats.Select(s => s.ChildId).ToHashSet();
        var allMembers = await _context.TeamMembers
            .Include(tm => tm.User)
            .Where(tm => tm.TeamId == teamId && tm.User.IsActive)
            .Select(tm => new { tm.UserId, tm.User.FirstName, tm.User.LastName })
            .ToListAsync();

        foreach (var member in allMembers.Where(m => !membersWithRecords.Contains(m.UserId)))
        {
            stats.Add(new MemberAttendanceStatsDto(
                member.UserId,
                $"{member.FirstName} {member.LastName}",
                0, 0, 0, 0
            ));
        }

        return stats.OrderByDescending(s => s.AttendancePercentage).ToList();
    }

    public async Task<AttendanceRecordDto> MarkAttendanceAsync(MarkAttendanceRequest request, ClaimsPrincipal user)
    {
        var userId = user.GetRequiredUserId();

        var training = await _context.TrainingEvents.FindAsync(request.TrainingEventId)
            ?? throw new KeyNotFoundException("Tréning neexistuje");

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(training.TeamId))
            throw new UnauthorizedAccessException();

        if (training.IsLocked)
            throw new InvalidOperationException("Dochádzka pre tento tréning je uzamknutá");

        var child = await _context.Users.FindAsync(request.ChildId)
            ?? throw new KeyNotFoundException("Člen neexistuje");

        var existingRecord = await _context.AttendanceRecords
            .FirstOrDefaultAsync(ar => ar.TrainingEventId == request.TrainingEventId && ar.ChildId == request.ChildId);

        if (existingRecord != null)
        {
            existingRecord.Status = (Data.Entities.AttendanceStatus)(int)request.Status;
            existingRecord.Note = request.Note;
            existingRecord.CoachComment = request.CoachComment;
            existingRecord.MarkedByUserId = userId;
            existingRecord.RecordedAt = DateTime.UtcNow;
        }
        else
        {
            existingRecord = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                TrainingEventId = request.TrainingEventId,
                ChildId = request.ChildId,
                Status = (Data.Entities.AttendanceStatus)(int)request.Status,
                Note = request.Note,
                CoachComment = request.CoachComment,
                MarkedByUserId = userId,
                RecordedAt = DateTime.UtcNow
            };
            _context.AttendanceRecords.Add(existingRecord);
        }

        await _context.SaveChangesAsync();
        _auditService.Log("MarkAttendance", "Training", request.TrainingEventId.ToString(), user, new { request.ChildId, request.Status });

        return new AttendanceRecordDto(
            existingRecord.Id,
            existingRecord.TrainingEventId,
            training.Title,
            training.StartTime,
            existingRecord.ChildId,
            $"{child.FirstName} {child.LastName}",
            (AttendanceStatus)(int)existingRecord.Status,
            existingRecord.Note,
            existingRecord.CoachComment,
            existingRecord.RecordedAt
        );
    }

    public async Task BulkMarkAttendanceAsync(BulkMarkAttendanceRequest request, ClaimsPrincipal user)
    {
        var userId = user.GetRequiredUserId();

        var training = await _context.TrainingEvents.FindAsync(request.TrainingEventId)
            ?? throw new KeyNotFoundException("Tréning neexistuje");

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(training.TeamId))
            throw new UnauthorizedAccessException();

        if (training.IsLocked)
            throw new InvalidOperationException("Dochádzka pre tento tréning je uzamknutá");

        foreach (var entry in request.Entries)
        {
            var existingRecord = await _context.AttendanceRecords
                .FirstOrDefaultAsync(ar => ar.TrainingEventId == request.TrainingEventId && ar.ChildId == entry.ChildId);

            if (existingRecord != null)
            {
                existingRecord.Status = (Data.Entities.AttendanceStatus)(int)entry.Status;
                existingRecord.Note = entry.Note;
                existingRecord.MarkedByUserId = userId;
                existingRecord.RecordedAt = DateTime.UtcNow;
            }
            else
            {
                _context.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TrainingEventId = request.TrainingEventId,
                    ChildId = entry.ChildId,
                    Status = (Data.Entities.AttendanceStatus)(int)entry.Status,
                    Note = entry.Note,
                    MarkedByUserId = userId,
                    RecordedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
        _auditService.Log("BulkMarkAttendance", "Training", request.TrainingEventId.ToString(), user, new { Count = request.Entries.Count });
    }

    public async Task AutoCompleteForTrainingAsync(Guid trainingEventId, bool setLocked)
    {
        var training = await _context.TrainingEvents
            .FirstOrDefaultAsync(t => t.Id == trainingEventId);

        if (training == null || DateTime.UtcNow < training.StartTime.AddMinutes(10))
            return;

        var teamMemberIds = await _context.TeamMembers
            .Where(tm => tm.TeamId == training.TeamId && tm.JoinedAt <= training.StartTime)
            .Select(tm => tm.UserId)
            .ToListAsync();

        if (!teamMemberIds.Any())
        {
            if (setLocked) { training.IsLocked = true; await _context.SaveChangesAsync(); }
            return;
        }

        var existingChildIds = await _context.AttendanceRecords
            .Where(ar => ar.TrainingEventId == trainingEventId)
            .Select(ar => ar.ChildId)
            .ToListAsync();

        var missingIds = teamMemberIds.Except(existingChildIds).ToList();

        if (missingIds.Any())
        {
            var excuses = await _context.AbsenceRequests
                .Where(ar => missingIds.Contains(ar.ChildId)
                             && ar.Status == AbsenceRequestStatus.Received
                             && (ar.TrainingEventId == trainingEventId
                                 || (ar.DateFrom.HasValue && ar.DateTo.HasValue
                                     && ar.DateFrom.Value.Date <= training.StartTime.Date
                                     && ar.DateTo.Value.Date >= training.StartTime.Date)))
                .Select(ar => ar.ChildId)
                .Distinct()
                .ToListAsync();

            foreach (var childId in missingIds)
            {
                _context.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TrainingEventId = trainingEventId,
                    ChildId = childId,
                    Status = excuses.Contains(childId) ? Data.Entities.AttendanceStatus.Excused : Data.Entities.AttendanceStatus.Present,
                    RecordedAt = DateTime.UtcNow
                });
            }
        }

        if (setLocked)
            training.IsLocked = true;

        await _context.SaveChangesAsync();
    }
}
