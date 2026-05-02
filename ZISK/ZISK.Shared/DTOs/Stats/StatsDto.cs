namespace ZISK.Shared.DTOs.Stats
{
    public record DashboardStatsDto(
        int TotalTeams,
        int ActiveTeams,
        int TotalMembers,
        int ActiveMembers,
        int TotalUsers,
        int PendingExcuses,
        AttendanceStatsDto AttendanceStats
    );

    public record AttendanceStatsDto(
        decimal PresentPercent,
        decimal AbsentPercent,
        decimal ExcusedPercent
    );

    public record TeamStatsDto(
        Guid TeamId,
        string TeamName,
        int MemberCount,
        bool IsActive,
        int TrainingCount,
        decimal AverageAttendance
    );

    public record TrainingTypeStatDto(
        string Type,
        int Count
    );

    public record AttendanceTrendPointDto(
        DateOnly Date,
        decimal PresentPercent,
        int TotalRecords
    );
}