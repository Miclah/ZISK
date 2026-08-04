using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Excuses;

public record UpdateExcuseStatusRequest(
    [StringLength(500, ErrorMessage = "validation.note.maxLength500")]
    string? ReviewNote
);
