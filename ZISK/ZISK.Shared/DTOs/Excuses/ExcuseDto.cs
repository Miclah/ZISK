using ZISK.Shared.Enums;

namespace ZISK.Shared.DTOs.Excuses;

public record ExcuseDto(
    Guid Id,
    Guid ChildId,
    string ChildName,
    Guid? TrainingEventId,
    string? TrainingName,
    DateTime? DateFrom,
    DateTime? DateTo,
    string? Reason,
    string? Note,
    ExcuseStatus Status,
    DateTime CreatedAt,
    string TeamName
);

public record ExcuseListDto(
    Guid Id,
    string ChildName,
    string TeamName,
    DateTime? DateFrom,
    DateTime? DateTo,
    string? Reason,
    ExcuseStatus Status,
    DateTime CreatedAt
);