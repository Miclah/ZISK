using Refit;
using ZISK.Shared.DTOs.Users;

namespace ZISK.Client.Services;

public interface IMeApi
{
    [Get("/api/me")]
    Task<MyProfileDto> GetMyProfileAsync();

    [Put("/api/me/contact")]
    Task<MyProfileDto> UpdateMyContactAsync([Body] UpdateMyContactRequest request);
}
