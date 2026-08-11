using System.Security.Claims;
using ZISK.Shared.DTOs.Announcements;
using TargetAudience = ZISK.Shared.Enums.TargetAudience;

namespace ZISK.Services;

public interface IAnnouncementService
{
    Task<List<AnnouncementListDto>> GetAnnouncementsAsync(Guid? teamId, TargetAudience? audience, ClaimsPrincipal user);
    Task<AnnouncementDto> GetAnnouncementAsync(Guid id, ClaimsPrincipal user);
    Task<AnnouncementDto> CreateAnnouncementAsync(CreateAnnouncementRequest request, ClaimsPrincipal user);
    Task UpdateAnnouncementAsync(Guid id, UpdateAnnouncementRequest request, ClaimsPrincipal user);
    Task DeleteAnnouncementAsync(Guid id, ClaimsPrincipal user);
    Task<int> GetUnreadCountAsync();
    Task<AttachmentDto> UploadAttachmentAsync(Guid announcementId, IFormFile file);
    Task<(string FullPath, string ContentType, string FileName)> GetAttachmentFileAsync(Guid announcementId, Guid attachmentId, ClaimsPrincipal user);
    Task DeleteAttachmentAsync(Guid announcementId, Guid attachmentId);
}
