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
    string Title,
    string Content,
    Guid? TargetTeamId,
    TargetAudience TargetAudience,
    AnnouncementPriority Priority,
    bool IsPinned,
    DateTime? ValidUntil
);

public record UpdateAnnouncementRequest(
    string Title,
    string Content,
    Guid? TargetTeamId,
    TargetAudience TargetAudience,
    AnnouncementPriority Priority,
    bool IsPinned,
    DateTime? ValidUntil
);
