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
    [Required(ErrorMessage = "validation.cancelReason.required")]
    [StringLength(500, ErrorMessage = "validation.cancelReason.maxLength500")]
    string Reason
);

public record CreateTrainingEventRequest(
    [Required(ErrorMessage = "validation.team.required")]
    Guid TeamId,

    [Required(ErrorMessage = "validation.name.required")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "validation.name.length2to200")]
    string Title,

    DateTime StartTime,
    DateTime EndTime,

    [StringLength(200, ErrorMessage = "validation.location.maxLength200")]
    string? Location,

    TrainingType Type,

    [StringLength(1000, ErrorMessage = "validation.note.maxLength1000")]
    string? CoachNote
);

public record UpdateTrainingEventRequest(
    [Required(ErrorMessage = "validation.name.required")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "validation.name.length2to200")]
    string Title,

    DateTime StartTime,
    DateTime EndTime,

    [StringLength(200, ErrorMessage = "validation.location.maxLength200")]
    string? Location,

    TrainingType Type,

    [StringLength(1000, ErrorMessage = "validation.note.maxLength1000")]
    string? CoachNote,

    bool IsLocked
);
