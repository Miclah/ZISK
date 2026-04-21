using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Excuses;

public record UpdateExcuseStatusRequest(
    [property: StringLength(500, ErrorMessage = "Poznámka môže mať max 500 znakov.")]
    string? ReviewNote
);