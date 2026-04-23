using ZISK.Data.Entities;

namespace ZISK.Services;

public enum InvitationIssueStatus
{
    Success,
    Forbidden,
    TooManyActive
}

public enum InvitationRedeemStatus
{
    Success,
    NotFound,
    Expired,
    TooManyAttempts,
    Invalid,
    AlreadyLinked,
    AlreadyUsed
}

public record InvitationIssueResult(InvitationIssueStatus Status, ParentInvitation? Invitation = null, string? PlainCode = null);
public record InvitationRedeemResult(InvitationRedeemStatus Status);

public interface IParentInvitationService
{
    Task<InvitationIssueResult> IssueEmailLinkAsync(string childUserId, string initiatorUserId, string targetEmail, CancellationToken ct = default);
    Task<InvitationIssueResult> IssueManualCodeAsync(string childUserId, string initiatorUserId, CancellationToken ct = default);
    Task<InvitationRedeemResult> RedeemEmailLinkAsync(string rawToken, string redeemerUserId, CancellationToken ct = default);
    Task<InvitationRedeemResult> RedeemManualCodeAsync(string rawCode, string redeemerUserId, CancellationToken ct = default);
}
