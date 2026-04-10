using System.ComponentModel.DataAnnotations;
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
    int Total,
    double AttendancePercentage
);

public record MemberAttendanceStatsDto(
    Guid ChildId,
    string ChildName,
    int Present,
    int Absent,
    int Excused,
    double AttendancePercentage
);

public record MarkAttendanceRequest(
    [property: Required(ErrorMessage = "Tréning je povinný.")]
    Guid TrainingEventId,

    [property: Required(ErrorMessage = "Člen je povinný.")]
    Guid ChildId,

    AttendanceStatus Status,

    [property: StringLength(500, ErrorMessage = "Poznámka môže mať max 500 znakov.")]
    string? Note,

    [property: StringLength(500, ErrorMessage = "Komentár trénera môže mať max 500 znakov.")]
    string? CoachComment
);

public record BulkMarkAttendanceRequest(
    [property: Required(ErrorMessage = "Tréning je povinný.")]
    Guid TrainingEventId,

    [property: Required(ErrorMessage = "Záznamy sú povinné.")]
    [property: MinLength(1, ErrorMessage = "Musí byť aspoň jeden záznam.")]
    List<AttendanceEntryDto> Entries
);

public record AttendanceEntryDto(
    Guid ChildId,
    AttendanceStatus Status,

    [property: StringLength(500, ErrorMessage = "Poznámka môže mať max 500 znakov.")]
    string? Note
);
