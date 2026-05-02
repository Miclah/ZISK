using ZISK.Shared.DTOs.Stats;

namespace ZISK.Services;

public interface IStatsService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync();
    Task<List<TeamStatsDto>> GetTeamStatsAsync();
    Task<AttendanceStatsDto> GetAttendanceStatsAsync(int days);
    Task<List<TrainingTypeStatDto>> GetTrainingTypeStatsAsync(int days);
    Task<List<AttendanceTrendPointDto>> GetAttendanceTrendAsync(int days);
}
