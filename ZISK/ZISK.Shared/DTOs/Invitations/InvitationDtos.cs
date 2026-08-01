using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Invitations;

public record SendEmailInvitationRequest(
    [Required(ErrorMessage = "Email je povinný.")][EmailAddress(ErrorMessage = "Neplatný formát emailu.")] string TargetEmail);

public record InvitationCodeResponse(string Code, DateTime ExpiresAt);

public record PendingInvitationDto(
    Guid InvitationId,
    string ChildUserId,
    string ChildName,
    string InitiatorName,
    DateTime ExpiresAt
);

public record RedeemCodeRequest([Required(ErrorMessage = "Kód je povinný.")] string Code);
