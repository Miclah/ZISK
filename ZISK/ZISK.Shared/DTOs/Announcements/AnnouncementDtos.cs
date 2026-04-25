using System.ComponentModel.DataAnnotations;
using ZISK.Shared.Enums;

namespace ZISK.Shared.DTOs.Announcements;

public record AnnouncementDto(
    Guid Id,
    string Title,
    string Content,
    Guid? TargetTeamId,
    string? TargetTeamName,
    TargetAudience TargetAudience,
    AnnouncementPriority Priority,
    bool IsPinned,
    DateTime? ValidUntil,
    string AuthorId,
    string AuthorName,
    DateTime PublishDate,
    DateTime? UpdatedAt,
    int ViewCount,
    bool IsRead,
    List<AttachmentDto> Attachments
);

public record AnnouncementListDto(
    Guid Id,
    string Title,
    string ContentPreview,
    string? TargetTeamName,
    TargetAudience TargetAudience,
    AnnouncementPriority Priority,
    bool IsPinned,
    DateTime? ValidUntil,
    string AuthorName,
    DateTime PublishDate,
    int AttachmentCount,
    bool IsRead
);

public record AttachmentDto(
    Guid Id,
    string FileName,
    string? ContentType,
    long FileSize
);

public record CreateAnnouncementRequest(
    [Required(ErrorMessage = "Názov je povinný.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Názov musí mať 2 – 200 znakov.")]
    string Title,

    [Required(ErrorMessage = "Obsah je povinný.")]
    [StringLength(5000, ErrorMessage = "Obsah môže mať max 5000 znakov.")]
    string Content,

    Guid? TargetTeamId,
    TargetAudience TargetAudience,
    AnnouncementPriority Priority,
    bool IsPinned,
    DateTime? ValidUntil
);

public record UpdateAnnouncementRequest(
    [Required(ErrorMessage = "Názov je povinný.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Názov musí mať 2 – 200 znakov.")]
    string Title,

    [Required(ErrorMessage = "Obsah je povinný.")]
    [StringLength(5000, ErrorMessage = "Obsah môže mať max 5000 znakov.")]
    string Content,

    Guid? TargetTeamId,
    TargetAudience TargetAudience,
    AnnouncementPriority Priority,
    bool IsPinned,
    DateTime? ValidUntil
);
