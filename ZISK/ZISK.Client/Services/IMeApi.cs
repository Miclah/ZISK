using Refit;
using ZISK.Shared.DTOs.Users;

namespace ZISK.Client.Services;

public interface IMeApi
{
    [Get("/api/me")]
    Task<MyProfileDto> GetMyProfileAsync();

    [Put("/api/me/contact")]
    Task<MyProfileDto> UpdateMyContactAsync([Body] UpdateMyContactRequest request);

    [Post("/api/me/change-password")]
    Task ChangeMyPasswordAsync([Body] ChangeMyPasswordRequest request);

    [Post("/api/me/change-email")]
    Task ChangeMyEmailAsync([Body] ChangeMyEmailRequest request);

    [Delete("/api/me")]
    Task DeleteMyAccountAsync([Body] DeleteMyAccountRequest request);
}
