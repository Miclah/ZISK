namespace ZISK.Shared.DTOs.Excuses;

public record UpdateExcuseRequest(
    DateTime? DateFrom,
    DateTime? DateTo,
    string Reason,
    string? Note
);
