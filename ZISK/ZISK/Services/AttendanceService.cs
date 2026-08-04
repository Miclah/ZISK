using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Extensions;
using ZISK.Helpers;
using ZISK.Shared.DTOs.Attendance;
using ZISK.Shared.Localization;
using AttendanceStatus = ZISK.Shared.Enums.AttendanceStatus;

namespace ZISK.Services;

public class AttendanceService : IAttendanceService
{
    private readonly ApplicationDbContext _context;
    private readonly ITeamAccessService _teamAccessService;
    private readonly IAuditService _auditService;
    private readonly ICurrentLanguage _currentLanguage;

    public AttendanceService(ApplicationDbContext context, ITeamAccessService teamAccessService, IAuditService auditService, ICurrentLanguage currentLanguage)
    {
        _context = context;
        _teamAccessService = teamAccessService;
        _auditService = auditService;
        _currentLanguage = currentLanguage;
    }

    public async Task<List<AttendanceRecordDto>> GetTrainingAttendanceAsync(Guid trainingEventId, ClaimsPrincipal user)
    {
        var training = await _context.TrainingEvents
            .Include(t => t.Team)
            .FirstOrDefaultAsync(t => t.Id == trainingEventId);

        if (training == null)
            throw new KeyNotFoundException(Translations.Get(_currentLanguage.Current, "errors.attendance.trainingNotFound"));

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(training.TeamId))
            throw new UnauthorizedAccessException();

        // Side effect on GET: ensures every team member has a record before the attendance list is shown to the coach.
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
            .Include(ar => ar.Child)
            .OrderByDescending(ar => ar.TrainingEvent.StartTime)
            .Select(ar => new UserAttendanceDto(
                ar.Id,
                ar.TrainingEventId,
                ar.TrainingEvent.Title,
                ar.TrainingEvent.StartTime,
                (AttendanceStatus)(int)ar.Status,
                ar.Note,
                ar.CoachComment,
                ar.ChildId,
                ar.Child.FirstName + " " + ar.Child.LastName
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

        // Members with no attendance records are missing from the GROUP BY result above.
        // They must be added manually with zero stats so they still appear in the overview.
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
            ?? throw new KeyNotFoundException(Translations.Get(_currentLanguage.Current, "errors.attendance.trainingNotFound"));

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(training.TeamId))
            throw new UnauthorizedAccessException();

        if (training.IsLocked)
            throw new InvalidOperationException(Translations.Get(_currentLanguage.Current, "errors.attendance.locked"));

        var child = await _context.Users.FindAsync(request.ChildId)
            ?? throw new KeyNotFoundException(Translations.Get(_currentLanguage.Current, "errors.attendance.memberNotFound"));

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
            ?? throw new KeyNotFoundException(Translations.Get(_currentLanguage.Current, "errors.attendance.trainingNotFound"));

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(training.TeamId))
            throw new UnauthorizedAccessException();

        if (training.IsLocked)
            throw new InvalidOperationException(Translations.Get(_currentLanguage.Current, "errors.attendance.locked"));

        // Load every existing record for this training up front. Looking each one up inside the loop
        // issued one round-trip per child, so marking a full team's attendance cost N queries.
        var entryChildIds = request.Entries.Select(e => e.ChildId).ToList();
        var existingRecords = await _context.AttendanceRecords
            .Where(ar => ar.TrainingEventId == request.TrainingEventId && entryChildIds.Contains(ar.ChildId))
            .ToDictionaryAsync(ar => ar.ChildId);

        foreach (var entry in request.Entries)
        {
            if (existingRecords.TryGetValue(entry.ChildId, out var existingRecord))
            {
                existingRecord.Status = (Data.Entities.AttendanceStatus)(int)entry.Status;
                existingRecord.Note = entry.Note;
                existingRecord.MarkedByUserId = userId;
                existingRecord.RecordedAt = DateTime.UtcNow;
            }
            else
            {
                var newRecord = new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TrainingEventId = request.TrainingEventId,
                    ChildId = entry.ChildId,
                    Status = (Data.Entities.AttendanceStatus)(int)entry.Status,
                    Note = entry.Note,
                    MarkedByUserId = userId,
                    RecordedAt = DateTime.UtcNow
                };
                _context.AttendanceRecords.Add(newRecord);

                // Track it so a repeated ChildId in the same payload updates this pending row instead of
                // adding a second one, which would violate the unique (TrainingEventId, ChildId) index.
                existingRecords[entry.ChildId] = newRecord;
            }
        }

        await _context.SaveChangesAsync();
        _auditService.Log("BulkMarkAttendance", "Training", request.TrainingEventId.ToString(), user, new { Count = request.Entries.Count });
    }

    public async Task AutoCompleteForTrainingAsync(Guid trainingEventId, bool setLocked)
    {
        var training = await _context.TrainingEvents
            .FirstOrDefaultAsync(t => t.Id == trainingEventId);

        // AddMinutes(10) is an intentional grace period — auto-complete does not fire until the training has actually started.
        if (training == null || DateTime.UtcNow < training.StartTime.AddMinutes(10))
            return;

        var teamMemberIds = await _context.TeamMembers
            .Where(tm => tm.TeamId == training.TeamId && tm.JoinedAt <= training.StartTime) // only members who were in the team before the training started
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
            // An excuse matches either a specific training ID or a date range
            // a parent can excuse an entire week without linking to individual training events.
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
                    Status = excuses.Contains(childId) ? Data.Entities.AttendanceStatus.Excused : Data.Entities.AttendanceStatus.Present, // default is Present (optimistic); coach corrects if needed
                    RecordedAt = DateTime.UtcNow
                });
            }
        }

        if (setLocked)
            training.IsLocked = true;

        await _context.SaveChangesAsync();
    }
}
