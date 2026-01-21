using ZISK.Shared.DTOs.Stats;
using Refit;

namespace ZISK.Client.Services
{
    public interface IStatsApi
    {
        [Get("/api/stats/dashboard")]
        Task<DashboardStatsDto> GetDashboardStatsAsync();

        [Get("/api/stats/teams")]
        Task<List<TeamStatsDto>> GetTeamStatsAsync();

        [Get("/api/stats/attendance")]
        Task<AttendanceStatsDto> GetAttendanceStatsAsync([Query] int? days = 30);
    }
}