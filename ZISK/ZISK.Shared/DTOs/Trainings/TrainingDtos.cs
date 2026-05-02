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
    bool IsLocked,
    bool IsCancelled,
    string? CancelledReason
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
    bool IsCancelled,
    string? CancelledReason,
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

public record CancelTrainingRequest(
    [Required(ErrorMessage = "Dôvod zrušenia je povinný.")]
    [StringLength(500, ErrorMessage = "Dôvod môže mať max 500 znakov.")]
    string Reason
);

public record CreateTrainingEventRequest(
    [Required(ErrorMessage = "Tím je povinný.")]
    Guid TeamId,

    [Required(ErrorMessage = "Názov je povinný.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Názov musí mať 2 – 200 znakov.")]
    string Title,

    DateTime StartTime,
    DateTime EndTime,

    [StringLength(200, ErrorMessage = "Miesto môže mať max 200 znakov.")]
    string? Location,

    TrainingType Type,

    [StringLength(1000, ErrorMessage = "Poznámka môže mať max 1000 znakov.")]
    string? CoachNote
);

public record UpdateTrainingEventRequest(
    [Required(ErrorMessage = "Názov je povinný.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Názov musí mať 2 – 200 znakov.")]
    string Title,

    DateTime StartTime,
    DateTime EndTime,

    [StringLength(200, ErrorMessage = "Miesto môže mať max 200 znakov.")]
    string? Location,

    TrainingType Type,

    [StringLength(1000, ErrorMessage = "Poznámka môže mať max 1000 znakov.")]
    string? CoachNote,

    bool IsLocked
);
