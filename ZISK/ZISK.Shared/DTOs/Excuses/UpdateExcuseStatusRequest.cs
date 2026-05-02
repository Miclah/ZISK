using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Excuses;

public record UpdateExcuseStatusRequest(
    [StringLength(500, ErrorMessage = "Poznámka môže mať max 500 znakov.")]
    string? ReviewNote
);
