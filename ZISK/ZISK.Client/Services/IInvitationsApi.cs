using Refit;
using ZISK.Shared.DTOs.Invitations;

namespace ZISK.Client.Services;

public interface IInvitationsApi
{
    [Post("/api/children/{childId}/invitations/email")]
    Task SendEmailInvitationAsync(string childId, [Body] SendEmailInvitationRequest request);

    [Post("/api/children/{childId}/invitations/code")]
    Task<InvitationCodeResponse> GenerateCodeAsync(string childId);

    [Get("/api/invitations/pending")]
    Task<List<PendingInvitationDto>> GetPendingAsync();

    [Post("/api/invitations/accept")]
    Task AcceptAsync([Query] string token);

    [Post("/api/invitations/redeem-code")]
    Task RedeemCodeAsync([Body] RedeemCodeRequest request);
}
