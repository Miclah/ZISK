using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Seasons;

public record SeasonDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateSeasonRequest(
    [Required(ErrorMessage = "validation.seasonName.required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "validation.name.length2to100")]
    string Name,

    [Required(ErrorMessage = "validation.startDate.required")]
    DateOnly StartDate,

    [Required(ErrorMessage = "validation.endDate.required")]
    DateOnly EndDate
);

public record UpdateSeasonRequest(
    [Required(ErrorMessage = "validation.seasonName.required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "validation.name.length2to100")]
    string Name,

    [Required(ErrorMessage = "validation.startDate.required")]
    DateOnly StartDate,

    [Required(ErrorMessage = "validation.endDate.required")]
    DateOnly EndDate
);
