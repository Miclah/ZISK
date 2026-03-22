using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Stats;

namespace ZISK.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class StatsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StatsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats()
        {
            var teams = await _context.Teams.AsNoTracking().ToListAsync();
            var members = await _context.ChildProfiles.AsNoTracking().ToListAsync();
            var users = await _context.Users.CountAsync();
            var pendingExcuses = await _context.AbsenceRequests.CountAsync(ar => ar.Status == AbsenceRequestStatus.Received);

            var attendanceStats = await GetAttendanceStatsInternal(30);

            return Ok(new DashboardStatsDto(
                TotalTeams: teams.Count,
                ActiveTeams: teams.Count(t => t.IsActive),
                TotalMembers: members.Count,
                ActiveMembers: members.Count(m => m.IsActive),
                TotalUsers: users,
                PendingExcuses: pendingExcuses,
                AttendanceStats: attendanceStats
            ));
        }

        [HttpGet("teams")]
        public async Task<ActionResult<List<TeamStatsDto>>> GetTeamStats()
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

            var result = teams.Select(team =>
            {
                var hasAttendance = attendanceMap.TryGetValue(team.Id, out var attendance);
                var totalRecords = hasAttendance ? attendance!.Total : 0;
                var presentCount = hasAttendance ? attendance!.Present : 0;
                var avgAttendance = totalRecords > 0 ? (decimal)presentCount / totalRecords * 100 : 0;

                return new TeamStatsDto(
                    team.Id,
                    team.Name,
                    team.MemberCount,
                    team.IsActive,
                    team.TrainingCount,
                    Math.Round(avgAttendance, 1)
                );
            }).OrderByDescending(t => t.AverageAttendance).ThenBy(t => t.TeamName).ToList();

            return Ok(result);
        }

        [HttpGet("attendance")]
        public async Task<ActionResult<AttendanceStatsDto>> GetAttendanceStats([FromQuery] int days = 30)
        {
            var stats = await GetAttendanceStatsInternal(days);
            return Ok(stats);
        }

        private async Task<AttendanceStatsDto> GetAttendanceStatsInternal(int days)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var records = await _context.AttendanceRecords
                .Where(a => a.RecordedAt >= fromDate)
                .ToListAsync();

            var total = records.Count;
            if (total == 0)
            {
                return new AttendanceStatsDto(0, 0, 0);
            }

            var present = records.Count(r => r.Status == AttendanceStatus.Present);
            var absent = records.Count(r => r.Status == AttendanceStatus.Absent);
            var excused = records.Count(r => r.Status == AttendanceStatus.Excused);

            return new AttendanceStatsDto(
                Math.Round((decimal)present / total * 100, 1),
                Math.Round((decimal)absent / total * 100, 1),
                Math.Round((decimal)excused / total * 100, 1)
            );
        }
    }
}