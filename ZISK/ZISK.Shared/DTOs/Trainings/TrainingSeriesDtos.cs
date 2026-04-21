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
    [property: Required(ErrorMessage = "Tím je povinný.")]
    Guid TeamId,

    [property: Required(ErrorMessage = "Sezóna je povinná.")]
    Guid SeasonId,

    [property: Required(ErrorMessage = "Názov je povinný.")]
    [property: StringLength(200, MinimumLength = 2, ErrorMessage = "Názov musí mať 2–200 znakov.")]
    string Title,

    int DaysOfWeek,

    TimeOnly StartTime,

    TimeOnly EndTime,

    [property: StringLength(200, ErrorMessage = "Miesto môže mať max 200 znakov.")]
    string? Location,

    string Type,

    [property: StringLength(1000, ErrorMessage = "Poznámka môže mať max 1000 znakov.")]
    string? CoachNote
);

public record UpdateTrainingSeriesRequest(
    [property: Required(ErrorMessage = "Názov je povinný.")]
    [property: StringLength(200, MinimumLength = 2, ErrorMessage = "Názov musí mať 2–200 znakov.")]
    string Title,

    int DaysOfWeek,

    TimeOnly StartTime,

    TimeOnly EndTime,

    [property: StringLength(200, ErrorMessage = "Miesto môže mať max 200 znakov.")]
    string? Location,

    string Type,

    [property: StringLength(1000, ErrorMessage = "Poznámka môže mať max 1000 znakov.")]
    string? CoachNote,

    bool IsActive
);

public record GenerateInstancesRequest(
    [property: Required]
    DateOnly From,

    [property: Required]
    DateOnly To
);
