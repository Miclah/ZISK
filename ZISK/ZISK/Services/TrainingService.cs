using System.Security.Claims;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;
using ZISK.Shared.DTOs.Trainings;
using ZISK.Shared.Localization;
using AttendanceStatus = ZISK.Shared.Enums.AttendanceStatus;
using TrainingType = ZISK.Shared.Enums.TrainingType;

namespace ZISK.Services;

public class TrainingService : ITrainingService
{
    private readonly ApplicationDbContext _context;
    private readonly ITeamAccessService _teamAccessService;
    private readonly IAuditService _auditService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<TrainingService> _logger;
    private readonly ICurrentLanguage _currentLanguage;

    public TrainingService(
        ApplicationDbContext context,
        ITeamAccessService teamAccessService,
        IAuditService auditService,
        IEmailSender emailSender,
        ILogger<TrainingService> logger,
        ICurrentLanguage currentLanguage)
    {
        _context = context;
        _teamAccessService = teamAccessService;
        _auditService = auditService;
        _emailSender = emailSender;
        _logger = logger;
        _currentLanguage = currentLanguage;
    }

    public async Task<List<TrainingEventDto>> GetTrainingsAsync(Guid? teamId, DateTime? from, DateTime? to, ClaimsPrincipal user)
    {
        var query = _context.TrainingEvents.Include(t => t.Team).AsNoTracking();

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null)
            query = query.Where(t => accessibleTeamIds.Contains(t.TeamId));

        if (teamId.HasValue) query = query.Where(t => t.TeamId == teamId.Value);
        if (from.HasValue) query = query.Where(t => t.StartTime >= from.Value);
        if (to.HasValue) query = query.Where(t => t.StartTime <= to.Value);

        return await query
            .OrderByDescending(t => t.StartTime)
            .Select(t => new TrainingEventDto(
                t.Id, t.TeamId, t.Team.Name, t.Title, t.StartTime, t.EndTime,
                t.Location, (TrainingType)(int)t.Type, t.CoachNote, t.IsLocked,
                t.IsCancelled, t.CancelledReason))
            .ToListAsync();
    }

    public async Task<TrainingEventDetailDto> GetTrainingAsync(Guid id, ClaimsPrincipal user)
    {
        var training = await _context.TrainingEvents
            .Include(t => t.Team)
            .Include(t => t.AttendanceRecords).ThenInclude(ar => ar.Child)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException();

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(training.TeamId))
            throw new UnauthorizedAccessException();

        var teamMembers = await _context.TeamMembers
            .Include(tm => tm.User)
            .Where(tm => tm.TeamId == training.TeamId)
            .ToListAsync();

        var excuses = await _context.AbsenceRequests
            .Where(ar => ar.TrainingEventId == id && ar.Status == AbsenceRequestStatus.Received)
            .ToListAsync();

        // Pre-index into dictionaries so the Select below does O(1) lookups per member instead of O(n) scans.
        var attendanceByChild = training.AttendanceRecords.ToDictionary(ar => ar.ChildId);
        var excuseByChild = excuses.ToDictionary(e => e.ChildId);

        var attendance = teamMembers.Select(member =>
        {
            attendanceByChild.TryGetValue(member.UserId, out var record);
            excuseByChild.TryGetValue(member.UserId, out var excuse);

            return new TrainingAttendanceDto(
                member.UserId,
                $"{member.User.FirstName} {member.User.LastName}",
                record != null ? (AttendanceStatus)(int)record.Status : AttendanceStatus.Absent,
                record?.Note,
                record?.CoachComment,
                excuse != null,
                excuse?.Reason
            );
        }).OrderBy(a => a.ChildName).ToList();

        return new TrainingEventDetailDto(
            training.Id, training.TeamId, training.Team.Name, training.Title,
            training.StartTime, training.EndTime, training.Location,
            (TrainingType)(int)training.Type, training.CoachNote, training.IsLocked,
            training.IsCancelled, training.CancelledReason,
            training.CreatedAt, attendance
        );
    }

    public async Task<TrainingEventDto> CreateTrainingAsync(CreateTrainingEventRequest request, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length < 3 || request.Title.Length > 100)
            throw new ArgumentException(Translations.Get(_currentLanguage.Current, "errors.training.nameLength"));

        if (request.EndTime <= request.StartTime)
            throw new ArgumentException(Translations.Get(_currentLanguage.Current, "errors.training.endAfterStart"));

        if (request.Location != null && request.Location.Length > 200)
            throw new ArgumentException(Translations.Get(_currentLanguage.Current, "errors.training.locationMaxLength200"));

        var team = await _context.Teams.FindAsync(request.TeamId)
            ?? throw new ArgumentException(Translations.Get(_currentLanguage.Current, "errors.training.teamNotFound"));

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(request.TeamId))
            throw new UnauthorizedAccessException();

        var trainingDate = DateOnly.FromDateTime(request.StartTime);
        // Season resolution cascade: (1) season whose date range contains the training date (prefer active),
        // (2) any active season, (3) most recent season by start date, (4) throw if no season exists at all.
        var season = await _context.Seasons
                         .Where(s => s.StartDate <= trainingDate && s.EndDate >= trainingDate)
                         .OrderByDescending(s => s.IsActive)
                         .FirstOrDefaultAsync()
                     ?? await _context.Seasons.FirstOrDefaultAsync(s => s.IsActive)
                     ?? await _context.Seasons.OrderByDescending(s => s.StartDate).FirstOrDefaultAsync()
                     ?? throw new InvalidOperationException(Translations.Get(_currentLanguage.Current, "errors.training.noSeasonDefined"));

        var training = new TrainingEvent
        {
            Id = Guid.NewGuid(),
            TeamId = request.TeamId,
            SeasonId = season.Id,
            Title = request.Title,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Location = request.Location,
            Type = (Data.Entities.TrainingType)(int)request.Type,
            CoachNote = request.CoachNote,
            CreatedAt = DateTime.UtcNow
        };

        _context.TrainingEvents.Add(training);
        await _context.SaveChangesAsync();
        _auditService.Log("Create", "Training", training.Id.ToString(), user, new { training.TeamId, training.Title, training.StartTime });

        return new TrainingEventDto(
            training.Id, training.TeamId, team.Name, training.Title,
            training.StartTime, training.EndTime, training.Location,
            (TrainingType)(int)training.Type, training.CoachNote, training.IsLocked,
            training.IsCancelled, training.CancelledReason
        );
    }

    public async Task UpdateTrainingAsync(Guid id, UpdateTrainingEventRequest request, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length < 3 || request.Title.Length > 100)
            throw new ArgumentException(Translations.Get(_currentLanguage.Current, "errors.training.nameLength"));

        if (request.EndTime <= request.StartTime)
            throw new ArgumentException(Translations.Get(_currentLanguage.Current, "errors.training.endAfterStart"));

        if (request.Location != null && request.Location.Length > 200)
            throw new ArgumentException(Translations.Get(_currentLanguage.Current, "errors.training.locationMaxLength200"));

        var training = await _context.TrainingEvents.FindAsync(id)
            ?? throw new KeyNotFoundException();

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(training.TeamId))
            throw new UnauthorizedAccessException();

        training.Title = request.Title;
        training.StartTime = request.StartTime;
        training.EndTime = request.EndTime;
        training.Location = request.Location;
        training.Type = (Data.Entities.TrainingType)(int)request.Type;
        training.CoachNote = request.CoachNote;
        training.IsLocked = request.IsLocked;

        await _context.SaveChangesAsync();
        _auditService.Log("Update", "Training", training.Id.ToString(), user, new { training.Title, training.StartTime, training.IsLocked });
    }

    public async Task CancelTrainingAsync(Guid id, CancelTrainingRequest request, ClaimsPrincipal user)
    {
        var training = await _context.TrainingEvents
            .Include(t => t.Team)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException();

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(training.TeamId))
            throw new UnauthorizedAccessException();

        training.IsCancelled = true;
        training.CancelledReason = request.Reason;
        await _context.SaveChangesAsync();
        _auditService.Log("Cancel", "Training", training.Id.ToString(), user, new { training.Title, training.StartTime, request.Reason });

        await NotifyCancellationAsync(training);
    }

    private async Task NotifyCancellationAsync(TrainingEvent training)
    {
        var memberIds = await _context.TeamMembers
            .Where(tm => tm.TeamId == training.TeamId)
            .Select(tm => tm.UserId)
            .ToListAsync();

        var parentIds = await _context.ParentChildren
            .Where(pc => memberIds.Contains(pc.ChildId))
            .Select(pc => pc.ParentId)
            .ToListAsync();

        var recipientEmails = await _context.Users
            .Where(u => (memberIds.Contains(u.Id) || parentIds.Contains(u.Id)) && u.Email != null)
            .Select(u => u.Email!)
            .Distinct()
            .ToListAsync();

        var subject = $"Tréning zrušený: {training.Title}";
        var body = $"Tréning \"{training.Title}\" naplánovaný na {training.StartTime:d.M.yyyy HH:mm} bol zrušený."
            + (string.IsNullOrWhiteSpace(training.CancelledReason) ? "" : $" Dôvod: {training.CancelledReason}");

        foreach (var email in recipientEmails)
        {
            try
            {
                await _emailSender.SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                // A single failed notification must not roll back the cancellation or block the rest.
                _logger.LogWarning(ex, "Nepodarilo sa odoslať e-mail o zrušení tréningu {TrainingId} na {Email}", training.Id, email);
            }
        }
    }

    public async Task LockTrainingAsync(Guid id, ClaimsPrincipal user)
    {
        var training = await _context.TrainingEvents.FindAsync(id) ?? throw new KeyNotFoundException();
        training.IsLocked = true;
        await _context.SaveChangesAsync();
        _auditService.Log("Lock", "Training", training.Id.ToString(), user);
    }

    public async Task UnlockTrainingAsync(Guid id, ClaimsPrincipal user)
    {
        var training = await _context.TrainingEvents.FindAsync(id) ?? throw new KeyNotFoundException();
        training.IsLocked = false;
        await _context.SaveChangesAsync();
        _auditService.Log("Unlock", "Training", training.Id.ToString(), user);
    }

    public async Task DeleteTrainingAsync(Guid id, ClaimsPrincipal user)
    {
        var training = await _context.TrainingEvents.FindAsync(id) ?? throw new KeyNotFoundException();
        _context.TrainingEvents.Remove(training);
        await _context.SaveChangesAsync();
        _auditService.Log("Delete", "Training", training.Id.ToString(), user);
    }
}
