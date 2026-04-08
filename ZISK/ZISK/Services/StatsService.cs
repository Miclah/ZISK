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
        var members = await _context.ChildProfiles.AsNoTracking().ToListAsync();
        var users = await _context.Users.CountAsync();
        var pendingExcuses = await _context.AbsenceRequests.CountAsync(ar => ar.Status == AbsenceRequestStatus.Received);

        var attendanceStats = await GetAttendanceStatsInternalAsync(30);

        return new DashboardStatsDto(
            TotalTeams: teams.Count,
            ActiveTeams: teams.Count(t => t.IsActive),
            TotalMembers: members.Count,
            ActiveMembers: members.Count(m => m.IsActive),
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
                MemberCount = t.Members.Count(m => m.IsActive),
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
