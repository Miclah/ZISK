using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Helpers;
using ZISK.Shared.DTOs.Stats;

namespace ZISK.Services;

public class StatsService : IStatsService
{
    private readonly ApplicationDbContext _context;

    public StatsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        var teams = await _context.Teams.AsNoTracking().ToListAsync();
        var totalMembers = await _context.TeamMembers.AsNoTracking().Select(tm => tm.UserId).Distinct().CountAsync();
        var activeMembers = await _context.TeamMembers.AsNoTracking()
            .Where(tm => tm.User.IsActive)
            .Select(tm => tm.UserId)
            .Distinct()
            .CountAsync();
        var users = await _context.Users.CountAsync();
        var pendingExcuses = await _context.AbsenceRequests.CountAsync(ar => ar.Status == AbsenceRequestStatus.Received);

        var attendanceStats = await GetAttendanceStatsInternalAsync(30);

        return new DashboardStatsDto(
            TotalTeams: teams.Count,
            ActiveTeams: teams.Count(t => t.IsActive),
            TotalMembers: totalMembers,
            ActiveMembers: activeMembers,
            TotalUsers: users,
            PendingExcuses: pendingExcuses,
            AttendanceStats: attendanceStats
        );
    }

    public async Task<List<TeamStatsDto>> GetTeamStatsAsync()
    {
        var teams = await _context.Teams
            .AsNoTracking()
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.IsActive,
                MemberCount = t.Memberships.Count,
                TrainingCount = t.TrainingEvents.Count
            })
            .ToListAsync();

        var teamIds = teams.Select(t => t.Id).ToList();

        var attendanceByTeam = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(a => teamIds.Contains(a.TrainingEvent.TeamId))
            .GroupBy(a => a.TrainingEvent.TeamId)
            .Select(g => new
            {
                TeamId = g.Key,
                Total = g.Count(),
                Present = g.Count(x => x.Status == AttendanceStatus.Present)
            })
            .ToListAsync();

        var attendanceMap = attendanceByTeam.ToDictionary(x => x.TeamId, x => x);

        return teams.Select(team =>
        {
            var hasAttendance = attendanceMap.TryGetValue(team.Id, out var attendance);
            var totalRecords = hasAttendance ? attendance!.Total : 0;
            var presentCount = hasAttendance ? attendance!.Present : 0;

            return new TeamStatsDto(
                team.Id,
                team.Name,
                team.MemberCount,
                team.IsActive,
                team.TrainingCount,
                AttendanceCalculator.CalculatePercentageDecimal(presentCount, totalRecords)
            );
        }).OrderByDescending(t => t.AverageAttendance).ThenBy(t => t.TeamName).ToList();
    }

    public async Task<AttendanceStatsDto> GetAttendanceStatsAsync(int days)
    {
        return await GetAttendanceStatsInternalAsync(days);
    }

    public async Task<List<TrainingTypeStatDto>> GetTrainingTypeStatsAsync(int days)
    {
        var fromDate = DateTime.UtcNow.AddDays(-days);
        var grouped = await _context.TrainingEvents
            .AsNoTracking()
            .Where(t => t.StartTime >= fromDate)
            .GroupBy(t => t.Type)
            .Select(g => new TrainingTypeStatDto(g.Key.ToString(), g.Count()))
            .ToListAsync();

        return grouped.OrderByDescending(t => t.Count).ToList();
    }

    public async Task<List<AttendanceTrendPointDto>> GetAttendanceTrendAsync(int days)
    {
        var fromDate = DateTime.UtcNow.AddDays(-days).Date;
        var records = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.RecordedAt >= fromDate)
            .Select(a => new { a.RecordedAt, a.Status })
            .ToListAsync();

        
        // More than 60 days would produce 60+ data points on a daily chart, which is unreadable — switch to weekly buckets automatically.
        bool weekly = days > 60;

        DateOnly Bucket(DateTime dt)
        {
            var d = DateOnly.FromDateTime(dt);
            if (!weekly) return d;
            var diff = ((int)d.DayOfWeek + 6) % 7; // .NET DayOfWeek starts on Sunday (0); this formula shifts it to Monday (0) per ISO 8601
            return d.AddDays(-diff);
        }

        var result = records
            .GroupBy(r => Bucket(r.RecordedAt))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var total = g.Count();
                var present = g.Count(x => x.Status == AttendanceStatus.Present);
                return new AttendanceTrendPointDto(
                    g.Key,
                    AttendanceCalculator.CalculatePercentageDecimal(present, total),
                    total);
            })
            .ToList();

        return result;
    }

    private async Task<AttendanceStatsDto> GetAttendanceStatsInternalAsync(int days)
    {
        var fromDate = DateTime.UtcNow.AddDays(-days);
        var records = await _context.AttendanceRecords
            .Where(a => a.RecordedAt >= fromDate)
            .ToListAsync();

        var total = records.Count;
        if (total == 0)
            return new AttendanceStatsDto(0, 0, 0);

        var present = records.Count(r => r.Status == AttendanceStatus.Present);
        var absent = records.Count(r => r.Status == AttendanceStatus.Absent);
        var excused = records.Count(r => r.Status == AttendanceStatus.Excused);

        return new AttendanceStatsDto(
            AttendanceCalculator.CalculatePercentageDecimal(present, total),
            AttendanceCalculator.CalculatePercentageDecimal(absent, total),
            AttendanceCalculator.CalculatePercentageDecimal(excused, total)
        );
    }
}
