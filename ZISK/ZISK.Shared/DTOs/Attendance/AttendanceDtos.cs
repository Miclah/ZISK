using ZISK.Shared.Enums;

namespace ZISK.Shared.DTOs.Attendance;

public record AttendanceRecordDto(
    Guid Id,
    Guid TrainingEventId,
    string TrainingName,
    DateTime TrainingDate,
    Guid ChildId,
    string ChildName,
    AttendanceStatus Status,
    string? Note,
    string? CoachComment,
    DateTime RecordedAt
);

public record UserAttendanceDto(
    Guid Id,
    Guid TrainingEventId,
    string TrainingName,
    DateTime Date,
    AttendanceStatus Status,
    string? Note,
    string? CoachComment
);

public record AttendanceStatsDto(
    int Present,
    int Absent,
    int Excused,
    int Late,
    int Total,
    double AttendancePercentage
);

public record MemberAttendanceStatsDto(
    Guid ChildId,
    string ChildName,
    int Present,
    int Absent,
    int Excused,
    int Late,
    double AttendancePercentage
);

public record MarkAttendanceRequest(
    Guid TrainingEventId,
    Guid ChildId,
    AttendanceStatus Status,
    string? Note,
    string? CoachComment
);

public record BulkMarkAttendanceRequest(
    Guid TrainingEventId,
    List<AttendanceEntryDto> Entries
);

public record AttendanceEntryDto(
    Guid ChildId,
    AttendanceStatus Status,
    string? Note
);
