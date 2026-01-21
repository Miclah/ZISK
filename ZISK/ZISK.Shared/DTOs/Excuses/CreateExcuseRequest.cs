namespace ZISK.Shared.DTOs.Excuses;

public record CreateExcuseRequest(
    Guid ChildId,
    Guid? TrainingEventId,
    DateTime? DateFrom,
    DateTime? DateTo,
    string Reason,
    string? Note
);