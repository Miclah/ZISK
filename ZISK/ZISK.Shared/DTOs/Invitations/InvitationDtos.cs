using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Invitations;

public record SendEmailInvitationRequest([Required][EmailAddress] string TargetEmail);

public record InvitationCodeResponse(string Code, DateTime ExpiresAt);

public record PendingInvitationDto(
    Guid InvitationId,
    string ChildUserId,
    string ChildName,
    string InitiatorName,
    DateTime ExpiresAt,
    string Token
);

public record RedeemCodeRequest([Required] string Code);
