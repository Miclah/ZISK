using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Trainings;

public record TrainingSeriesDto(
    Guid Id,
    Guid TeamId,
    string TeamName,
    string CoachId,
    string CoachName,
    Guid SeasonId,
    string SeasonName,
    string Title,
    int DaysOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Location,
    string Type,
    string? CoachNote,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateTrainingSeriesRequest(
    [Required(ErrorMessage = "validation.team.required")]
    Guid TeamId,

    [Required(ErrorMessage = "validation.season.required")]
    Guid SeasonId,

    [Required(ErrorMessage = "validation.name.required")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "validation.name.length2to200")]
    string Title,

    int DaysOfWeek,

    TimeOnly StartTime,

    TimeOnly EndTime,

    [StringLength(200, ErrorMessage = "validation.location.maxLength200")]
    string? Location,

    string Type,

    [StringLength(1000, ErrorMessage = "validation.note.maxLength1000")]
    string? CoachNote
);

public record UpdateTrainingSeriesRequest(
    [Required(ErrorMessage = "validation.name.required")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "validation.name.length2to200")]
    string Title,

    int DaysOfWeek,

    TimeOnly StartTime,

    TimeOnly EndTime,

    [StringLength(200, ErrorMessage = "validation.location.maxLength200")]
    string? Location,

    string Type,

    [StringLength(1000, ErrorMessage = "validation.note.maxLength1000")]
    string? CoachNote,

    bool IsActive
);

public record GenerateInstancesRequest(
    [Required(ErrorMessage = "validation.dateFrom.required")]
    DateOnly From,

    [Required(ErrorMessage = "validation.dateTo.required")]
    DateOnly To
);
