using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Trainings;

namespace ZISK.Services;

public class TrainingSeriesService : ITrainingSeriesService
{
    private readonly ApplicationDbContext _context;
    private readonly ITeamAccessService _teamAccessService;
    private readonly IAuditService _auditService;

    public TrainingSeriesService(
        ApplicationDbContext context,
        ITeamAccessService teamAccessService,
        IAuditService auditService)
    {
        _context = context;
        _teamAccessService = teamAccessService;
        _auditService = auditService;
    }

    public async Task<List<TrainingSeriesDto>> GetSeriesAsync(ClaimsPrincipal user, Guid? teamId = null, Guid? seasonId = null)
    {
        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);

        var query = _context.TrainingSeries
            .Include(ts => ts.Team)
            .Include(ts => ts.Coach)
            .Include(ts => ts.Season)
            .AsNoTracking();

        if (accessibleTeamIds is not null)
            query = query.Where(ts => accessibleTeamIds.Contains(ts.TeamId));
        if (teamId.HasValue)
            query = query.Where(ts => ts.TeamId == teamId.Value);
        if (seasonId.HasValue)
            query = query.Where(ts => ts.SeasonId == seasonId.Value);

        return await query.OrderBy(ts => ts.Title).Select(ts => ToDto(ts)).ToListAsync();
    }

    public async Task<TrainingSeriesDto> GetSeriesAsync(Guid id, ClaimsPrincipal user)
    {
        var series = await _context.TrainingSeries
            .Include(ts => ts.Team)
            .Include(ts => ts.Coach)
            .Include(ts => ts.Season)
            .AsNoTracking()
            .FirstOrDefaultAsync(ts => ts.Id == id)
            ?? throw new KeyNotFoundException();

        await EnsureTeamAccessAsync(series.TeamId, user);
        return ToDto(series);
    }

    public async Task<TrainingSeriesDto> CreateSeriesAsync(CreateTrainingSeriesRequest request, string coachId, ClaimsPrincipal user)
    {
        if (request.StartTime >= request.EndTime)
            throw new ArgumentException("Čas začiatku musí byť pred časom konca.");

        if (!await _context.Teams.AnyAsync(t => t.Id == request.TeamId))
            throw new ArgumentException("Tím neexistuje.");

        if (!await _context.Seasons.AnyAsync(s => s.Id == request.SeasonId))
            throw new ArgumentException("Sezóna neexistuje.");

        await EnsureTeamAccessAsync(request.TeamId, user);

        var series = new TrainingSeries
        {
            Id = Guid.NewGuid(),
            TeamId = request.TeamId,
            CoachId = coachId,
            SeasonId = request.SeasonId,
            Title = request.Title,
            DaysOfWeek = request.DaysOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Location = request.Location,
            Type = Enum.TryParse<TrainingType>(request.Type, out var type) ? type : TrainingType.Other,
            CoachNote = request.CoachNote,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TrainingSeries.Add(series);
        await _context.SaveChangesAsync();
        _auditService.Log("Create", "TrainingSeries", series.Id.ToString(), user, new { series.Title, series.TeamId });

        return await GetSeriesAsync(series.Id, user);
    }

    public async Task<TrainingSeriesDto> UpdateSeriesAsync(Guid id, UpdateTrainingSeriesRequest request, ClaimsPrincipal user)
    {
        if (request.StartTime >= request.EndTime)
            throw new ArgumentException("Čas začiatku musí byť pred časom konca.");

        var series = await _context.TrainingSeries.FindAsync(id) ?? throw new KeyNotFoundException();
        await EnsureTeamAccessAsync(series.TeamId, user);

        series.Title = request.Title;
        series.DaysOfWeek = request.DaysOfWeek;
        series.StartTime = request.StartTime;
        series.EndTime = request.EndTime;
        series.Location = request.Location;
        series.Type = Enum.TryParse<TrainingType>(request.Type, out var type) ? type : TrainingType.Other;
        series.CoachNote = request.CoachNote;
        series.IsActive = request.IsActive;

        await _context.SaveChangesAsync();
        _auditService.Log("Update", "TrainingSeries", series.Id.ToString(), user, new { series.Title, series.IsActive });

        return await GetSeriesAsync(id, user);
    }

    public async Task DeleteSeriesAsync(Guid id, ClaimsPrincipal user)
    {
        var series = await _context.TrainingSeries.FindAsync(id) ?? throw new KeyNotFoundException();
        await EnsureTeamAccessAsync(series.TeamId, user);

        _context.TrainingSeries.Remove(series);
        await _context.SaveChangesAsync();
        _auditService.Log("Delete", "TrainingSeries", series.Id.ToString(), user, new { series.Title, series.TeamId });
    }

    public async Task<int> GenerateInstancesAsync(Guid seriesId, DateOnly from, DateOnly to, ClaimsPrincipal user)
    {
        var series = await _context.TrainingSeries
            .Include(ts => ts.Season)
            .FirstOrDefaultAsync(ts => ts.Id == seriesId)
            ?? throw new KeyNotFoundException();

        await EnsureTeamAccessAsync(series.TeamId, user);

        if (from >= to)
            throw new ArgumentException("Dátum od musí byť pred dátumom do.");

        var weekdays = (Weekdays)series.DaysOfWeek; // DaysOfWeek is stored as an int bitmask in the DB; cast restores the [Flags] enum
        var existingDates = await _context.TrainingEvents
            .Where(te => te.SeriesId == seriesId)
            .Select(te => DateOnly.FromDateTime(te.StartTime))
            .ToListAsync();

        var existingSet = new HashSet<DateOnly>(existingDates); // O(1) lookup per iteration; List.Contains would be O(n) over potentially hundreds of dates
        var generated = 0;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var dayFlag = date.DayOfWeek switch
            {
                DayOfWeek.Monday => Weekdays.Monday,
                DayOfWeek.Tuesday => Weekdays.Tuesday,
                DayOfWeek.Wednesday => Weekdays.Wednesday,
                DayOfWeek.Thursday => Weekdays.Thursday,
                DayOfWeek.Friday => Weekdays.Friday,
                DayOfWeek.Saturday => Weekdays.Saturday,
                DayOfWeek.Sunday => Weekdays.Sunday,
                _ => Weekdays.None
            };

            if ((weekdays & dayFlag) == Weekdays.None || existingSet.Contains(date)) // bitwise AND; None=0 means this day is not included in the stored mask
                continue;

            var startDt = date.ToDateTime(series.StartTime);
            var endDt = date.ToDateTime(series.EndTime);

            _context.TrainingEvents.Add(new TrainingEvent
            {
                Id = Guid.NewGuid(),
                TeamId = series.TeamId,
                SeriesId = series.Id,
                SeasonId = series.SeasonId,
                Title = series.Title,
                StartTime = startDt,
                EndTime = endDt,
                Location = series.Location,
                Type = series.Type,
                CoachNote = series.CoachNote,
                CreatedAt = DateTime.UtcNow
            });

            generated++;
        }

        await _context.SaveChangesAsync();
        _auditService.Log("GenerateInstances", "TrainingSeries", series.Id.ToString(), user, new { generated, from, to });

        return generated;
    }

    private async Task EnsureTeamAccessAsync(Guid teamId, ClaimsPrincipal user)
    {
        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null && !accessibleTeamIds.Contains(teamId))
            throw new UnauthorizedAccessException();
    }

    private static TrainingSeriesDto ToDto(TrainingSeries ts) => new(
        ts.Id,
        ts.TeamId,
        ts.Team.Name,
        ts.CoachId,
        $"{ts.Coach.FirstName} {ts.Coach.LastName}",
        ts.SeasonId,
        ts.Season.Name,
        ts.Title,
        ts.DaysOfWeek,
        ts.StartTime,
        ts.EndTime,
        ts.Location,
        ts.Type.ToString(),
        ts.CoachNote,
        ts.IsActive,
        ts.CreatedAt
    );
}
