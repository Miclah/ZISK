using System.ComponentModel.DataAnnotations;
using ZISK.Shared.Enums;

namespace ZISK.Shared.DTOs.Trainings;

public record TrainingEventDto(
    Guid Id,
    Guid TeamId,
    string TeamName,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string? Location,
    TrainingType Type,
    string? CoachNote,
    bool IsLocked
);

public record TrainingEventDetailDto(
    Guid Id,
    Guid TeamId,
    string TeamName,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string? Location,
    TrainingType Type,
    string? CoachNote,
    bool IsLocked,
    DateTime CreatedAt,
    List<TrainingAttendanceDto> Attendance
);

public record TrainingAttendanceDto(
    string ChildId,
    string ChildName,
    AttendanceStatus Status,
    string? Note,
    string? CoachComment,
    bool HasExcuse,
    string? ExcuseReason
);

public record CreateTrainingEventRequest(
    [property: Required(ErrorMessage = "Tím je povinný.")]
    Guid TeamId,

    [property: Required(ErrorMessage = "Názov je povinný.")]
    [property: StringLength(200, MinimumLength = 2, ErrorMessage = "Názov musí mať 2 – 200 znakov.")]
    string Title,

    DateTime StartTime,
    DateTime EndTime,

    [property: StringLength(200, ErrorMessage = "Miesto môže mať max 200 znakov.")]
    string? Location,

    TrainingType Type,

    [property: StringLength(1000, ErrorMessage = "Poznámka môže mať max 1000 znakov.")]
    string? CoachNote
);

public record UpdateTrainingEventRequest(
    [property: Required(ErrorMessage = "Názov je povinný.")]
    [property: StringLength(200, MinimumLength = 2, ErrorMessage = "Názov musí mať 2 – 200 znakov.")]
    string Title,

    DateTime StartTime,
    DateTime EndTime,

    [property: StringLength(200, ErrorMessage = "Miesto môže mať max 200 znakov.")]
    string? Location,

    TrainingType Type,

    [property: StringLength(1000, ErrorMessage = "Poznámka môže mať max 1000 znakov.")]
    string? CoachNote,

    bool IsLocked
);
