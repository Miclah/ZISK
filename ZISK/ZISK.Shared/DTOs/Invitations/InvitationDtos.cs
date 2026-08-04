using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Invitations;

public record SendEmailInvitationRequest(
    [Required(ErrorMessage = "validation.email.required")][EmailAddress(ErrorMessage = "validation.email.invalidFormat")] string TargetEmail);

public record InvitationCodeResponse(string Code, DateTime ExpiresAt);

public record PendingInvitationDto(
    Guid InvitationId,
    string ChildUserId,
    string ChildName,
    string InitiatorName,
    DateTime ExpiresAt
);

public record RedeemCodeRequest([Required(ErrorMessage = "validation.code.required")] string Code);
