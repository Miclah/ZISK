using ZISK.Shared.Enums;

namespace ZISK.Shared.DTOs.Excuses;

public record UpdateExcuseStatusRequest(
    ExcuseStatus Status,
    string? ReviewNote
);