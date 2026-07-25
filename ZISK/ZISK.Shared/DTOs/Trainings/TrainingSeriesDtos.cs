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
    [Required(ErrorMessage = "Tím je povinný.")]
    Guid TeamId,

    [Required(ErrorMessage = "Sezóna je povinná.")]
    Guid SeasonId,

    [Required(ErrorMessage = "Názov je povinný.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Názov musí mať 2–200 znakov.")]
    string Title,

    int DaysOfWeek,

    TimeOnly StartTime,

    TimeOnly EndTime,

    [StringLength(200, ErrorMessage = "Miesto môže mať max 200 znakov.")]
    string? Location,

    string Type,

    [StringLength(1000, ErrorMessage = "Poznámka môže mať max 1000 znakov.")]
    string? CoachNote
);

public record UpdateTrainingSeriesRequest(
    [Required(ErrorMessage = "Názov je povinný.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Názov musí mať 2–200 znakov.")]
    string Title,

    int DaysOfWeek,

    TimeOnly StartTime,

    TimeOnly EndTime,

    [StringLength(200, ErrorMessage = "Miesto môže mať max 200 znakov.")]
    string? Location,

    string Type,

    [StringLength(1000, ErrorMessage = "Poznámka môže mať max 1000 znakov.")]
    string? CoachNote,

    bool IsActive
);

public record GenerateInstancesRequest(
    [Required]
    DateOnly From,

    [Required]
    DateOnly To
);
