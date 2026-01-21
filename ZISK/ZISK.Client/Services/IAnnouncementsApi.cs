using Refit;
using ZISK.Shared.DTOs.Announcements;
using ZISK.Shared.Enums;

namespace ZISK.Client.Services;

public interface IAnnouncementsApi
{
    [Get("/api/announcements")]
    Task<List<AnnouncementListDto>> GetAnnouncementsAsync(
        [Query] Guid? teamId = null, 
        [Query] TargetAudience? audience = null);

    [Get("/api/announcements/{id}")]
    Task<AnnouncementDto> GetAnnouncementAsync(Guid id);

    [Get("/api/announcements/unread/count")]
    Task<int> GetUnreadCountAsync();

    [Post("/api/announcements")]
    Task<AnnouncementDto> CreateAnnouncementAsync([Body] CreateAnnouncementRequest request);

    [Put("/api/announcements/{id}")]
    Task UpdateAnnouncementAsync(Guid id, [Body] UpdateAnnouncementRequest request);

    [Delete("/api/announcements/{id}")]
    Task DeleteAnnouncementAsync(Guid id);
}
