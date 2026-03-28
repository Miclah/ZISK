namespace ZISK.Shared.DTOs.Stats
{
    public record DashboardStatsDto(
        int TotalTeams,
        int ActiveTeams,
        int TotalMembers,
        int ActiveMembers,
        int TotalUsers,
        int PendingExcuses,
        int TotalCoaches,
        AttendanceStatsDto AttendanceStats,
        List<ActivityDto> RecentActivities
    );

    public record AttendanceStatsDto(
        decimal PresentPercent,
        decimal AbsentPercent,
        decimal ExcusedPercent
    );

    public record ActivityDto(
        string Description,
        DateTime Timestamp,
        string Type
    );

    public record TeamStatsDto(
        Guid TeamId,
        string TeamName,
        int MemberCount,
        bool IsActive,
        int TrainingCount,
        decimal AverageAttendance
    );
}