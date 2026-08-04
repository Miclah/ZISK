using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Excuses;

public record UpdateExcuseRequest(
    DateTime? DateFrom,
    DateTime? DateTo,

    [Required(ErrorMessage = "validation.reason.required")]
    [StringLength(1000, MinimumLength = 3, ErrorMessage = "validation.reason.length3to1000")]
    string Reason,

    [StringLength(500, ErrorMessage = "validation.note.maxLength500")]
    string? Note
);
