using System.ComponentModel.DataAnnotations;
using ZISK.Shared.Enums;

namespace ZISK.Shared.DTOs.Attendance;

public record AttendanceRecordDto(
    Guid Id,
    Guid TrainingEventId,
    string TrainingName,
    DateTime TrainingDate,
    string ChildId,
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
    string? CoachComment,
    string ChildId,
    string ChildName
);

// Used in two different contexts: in AttendanceService the int fields are absolute counts,
// in StatsService they are percentages (decimal). The meaning depends on the caller — not obvious without reading both usages.
public record AttendanceStatsDto(
    int Present,
    int Absent,
    int Excused,
    int Total,
    double AttendancePercentage
);

public record MemberAttendanceStatsDto(
    string ChildId,
    string ChildName,
    int Present,
    int Absent,
    int Excused,
    double AttendancePercentage
);

public record MarkAttendanceRequest(
    [Required(ErrorMessage = "validation.training.required")]
    Guid TrainingEventId,

    [Required(ErrorMessage = "validation.member.required")]
    string ChildId,

    AttendanceStatus Status,

    [StringLength(500, ErrorMessage = "validation.note.maxLength500")]
    string? Note,

    [StringLength(500, ErrorMessage = "validation.coachComment.maxLength500")]
    string? CoachComment
);

public record BulkMarkAttendanceRequest(
    [Required(ErrorMessage = "validation.training.required")]
    Guid TrainingEventId,

    [Required(ErrorMessage = "validation.entries.required")]
    [MinLength(1, ErrorMessage = "validation.entries.minOne")]
    List<AttendanceEntryDto> Entries
);

public record AttendanceEntryDto(
    string ChildId,
    AttendanceStatus Status,

    [StringLength(500, ErrorMessage = "validation.note.maxLength500")]
    string? Note
);
