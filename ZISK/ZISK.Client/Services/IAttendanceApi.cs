using Refit;
using ZISK.Shared.DTOs.Attendance;
using ZISK.Shared.Enums;

namespace ZISK.Client.Services;

// Pomoc s AI
public interface IAttendanceApi
{
    [Get("/api/attendance/training/{trainingEventId}")]
    Task<List<AttendanceRecordDto>> GetTrainingAttendanceAsync(Guid trainingEventId);

    [Get("/api/attendance/my")]
    Task<List<UserAttendanceDto>> GetMyAttendanceAsync([Query] DateTime? from = null, [Query] DateTime? to = null);

    [Get("/api/attendance/stats/{childId}")]
    Task<AttendanceStatsDto> GetMemberStatsAsync(Guid childId, [Query] DateTime? from = null, [Query] DateTime? to = null);

    [Get("/api/attendance/stats/team/{teamId}")]
    Task<List<MemberAttendanceStatsDto>> GetTeamStatsAsync(Guid teamId, [Query] DateTime? from = null, [Query] DateTime? to = null);

    [Post("/api/attendance")]
    Task<AttendanceRecordDto> MarkAttendanceAsync([Body] MarkAttendanceRequest request);

    [Post("/api/attendance/bulk")]
    Task BulkMarkAttendanceAsync([Body] BulkMarkAttendanceRequest request);
}
