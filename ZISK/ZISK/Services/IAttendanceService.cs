using System.Security.Claims;
using ZISK.Shared.DTOs.Attendance;

namespace ZISK.Services;

public interface IAttendanceService
{
    Task<List<AttendanceRecordDto>> GetTrainingAttendanceAsync(Guid trainingEventId, ClaimsPrincipal user);
    Task<List<UserAttendanceDto>> GetMyAttendanceAsync(ClaimsPrincipal user, DateTime? from, DateTime? to);
    Task<AttendanceStatsDto> GetMemberStatsAsync(string childId, DateTime? from, DateTime? to);
    Task<List<MemberAttendanceStatsDto>> GetTeamStatsAsync(Guid teamId, DateTime? from, DateTime? to);
    Task<AttendanceRecordDto> MarkAttendanceAsync(MarkAttendanceRequest request, ClaimsPrincipal user);
    Task BulkMarkAttendanceAsync(BulkMarkAttendanceRequest request, ClaimsPrincipal user);
}
