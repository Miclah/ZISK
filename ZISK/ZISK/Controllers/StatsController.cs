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
            var teams = await _context.Teams.ToListAsync();
            var members = await _context.ChildProfiles.ToListAsync();
            var users = await _context.Users.CountAsync();
            var pendingExcuses = await _context.AbsenceRequests
                .CountAsync(a => a.Status == AbsenceRequestStatus.Pending);

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
                .Include(t => t.Members)
                .Include(t => t.TrainingEvents)
                .ToListAsync();

            var result = new List<TeamStatsDto>();

            foreach (var team in teams)
            {
                var trainingIds = team.TrainingEvents.Select(t => t.Id).ToList();
                var attendanceRecords = await _context.AttendanceRecords
                    .Where(a => trainingIds.Contains(a.TrainingEventId))
                    .ToListAsync();

                var totalRecords = attendanceRecords.Count;
                var presentCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.Present);
                var avgAttendance = totalRecords > 0 ? (decimal)presentCount / totalRecords * 100 : 0;

                result.Add(new TeamStatsDto(
                    team.Id,
                    team.Name,
                    team.Members.Count(m => m.IsActive),
                    team.IsActive,
                    team.TrainingEvents.Count,
                    Math.Round(avgAttendance, 1)
                ));
            }

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
                return new AttendanceStatsDto(0, 0, 0, 0);
            }

            var present = records.Count(r => r.Status == AttendanceStatus.Present);
            var absent = records.Count(r => r.Status == AttendanceStatus.Absent);
            var excused = records.Count(r => r.Status == AttendanceStatus.Excused);
            var late = records.Count(r => r.Status == AttendanceStatus.Late);

            return new AttendanceStatsDto(
                Math.Round((decimal)present / total * 100, 1),
                Math.Round((decimal)absent / total * 100, 1),
                Math.Round((decimal)excused / total * 100, 1),
                Math.Round((decimal)late / total * 100, 1)
            );
        }
    }
}