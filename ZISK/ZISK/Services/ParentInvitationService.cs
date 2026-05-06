using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;

namespace ZISK.Services;

public class ParentInvitationService : IParentInvitationService
{
    // Characters 0, O, I, 1, L are excluded — they look identical in most fonts and cause typos when entering codes manually.
    private const string ManualCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly ApplicationDbContext _context;

    public ParentInvitationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InvitationIssueResult> IssueEmailLinkAsync(string childUserId, string initiatorUserId, string targetEmail, CancellationToken ct = default)
    {
        if (!await IsParentOfChildAsync(initiatorUserId, childUserId, ct))
            return new InvitationIssueResult(InvitationIssueStatus.Forbidden);

        var activeCount = await _context.ParentInvitations
            .CountAsync(pi => pi.ChildUserId == childUserId && pi.UsedAt == null && pi.ExpiresAt > DateTime.UtcNow, ct);

        if (activeCount >= ParentInvitation.MaxActiveInvitations)
            return new InvitationIssueResult(InvitationIssueStatus.TooManyActive);

        var rawToken = GenerateSecureToken();
        var invitation = new ParentInvitation
        {
            ChildUserId = childUserId,
            InitiatorUserId = initiatorUserId,
            Type = ParentInvitationType.EmailLink,
            TargetEmail = targetEmail,
            CodeHash = HashCode(rawToken),
            ExpiresAt = DateTime.UtcNow.AddHours(ParentInvitation.LifetimeHours),
            CreatedAt = DateTime.UtcNow
        };

        _context.ParentInvitations.Add(invitation);
        await _context.SaveChangesAsync(ct);

        return new InvitationIssueResult(InvitationIssueStatus.Success, invitation, rawToken);
    }

    public async Task<InvitationIssueResult> IssueManualCodeAsync(string childUserId, string initiatorUserId, CancellationToken ct = default)
    {
        if (!await IsParentOfChildAsync(initiatorUserId, childUserId, ct))
            return new InvitationIssueResult(InvitationIssueStatus.Forbidden);

        var activeCount = await _context.ParentInvitations
            .CountAsync(pi => pi.ChildUserId == childUserId && pi.UsedAt == null && pi.ExpiresAt > DateTime.UtcNow, ct);

        if (activeCount >= ParentInvitation.MaxActiveInvitations)
            return new InvitationIssueResult(InvitationIssueStatus.TooManyActive);

        var rawCode = GenerateManualCode();
        var invitation = new ParentInvitation
        {
            ChildUserId = childUserId,
            InitiatorUserId = initiatorUserId,
            Type = ParentInvitationType.ManualCode,
            CodeHash = HashCode(NormalizeCode(rawCode)), // stored hash is always of the normalized (uppercase, digits only) form so lookup works regardless of how user typed it
            ExpiresAt = DateTime.UtcNow.AddHours(ParentInvitation.LifetimeHours),
            CreatedAt = DateTime.UtcNow
        };

        _context.ParentInvitations.Add(invitation);
        await _context.SaveChangesAsync(ct);

        var displayCode = $"{rawCode[..4]}-{rawCode[4..]}"; // display format XXXX-XXXX; the dash is stripped by NormalizeCode before hashing, so it never needs to be stored
        return new InvitationIssueResult(InvitationIssueStatus.Success, invitation, displayCode);
    }

    public async Task<InvitationRedeemResult> RedeemEmailLinkAsync(string rawToken, string redeemerUserId, CancellationToken ct = default)
    {
        var hash = HashCode(rawToken);
        var invitation = await _context.ParentInvitations
            .FirstOrDefaultAsync(pi => pi.CodeHash == hash && pi.Type == ParentInvitationType.EmailLink, ct);

        return await RedeemInternalAsync(invitation, redeemerUserId, ct);
    }

    public async Task<InvitationRedeemResult> RedeemManualCodeAsync(string rawCode, string redeemerUserId, CancellationToken ct = default)
    {
        var normalized = NormalizeCode(rawCode);
        var hash = HashCode(normalized);

        var invitation = await _context.ParentInvitations
            .Where(pi => pi.CodeHash == hash && pi.Type == ParentInvitationType.ManualCode
                         && pi.UsedAt == null && pi.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(pi => pi.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return await RedeemInternalAsync(invitation, redeemerUserId, ct);
    }

    private async Task<InvitationRedeemResult> RedeemInternalAsync(ParentInvitation? invitation, string redeemerUserId, CancellationToken ct)
    {
        if (invitation is null)
            return new InvitationRedeemResult(InvitationRedeemStatus.NotFound);

        if (invitation.UsedAt is not null)
            return new InvitationRedeemResult(InvitationRedeemStatus.AlreadyUsed);

        if (invitation.ExpiresAt < DateTime.UtcNow)
            return new InvitationRedeemResult(InvitationRedeemStatus.Expired);

        if (invitation.AttemptCount >= ParentInvitation.MaxAttempts)
            return new InvitationRedeemResult(InvitationRedeemStatus.TooManyAttempts);

        // AlreadyLinked check comes BEFORE incrementing AttemptCount so it does not consume an attempt on benign duplicate redemptions.
        var alreadyLinked = await _context.ParentChildren
            .AnyAsync(pc => pc.ParentId == redeemerUserId && pc.ChildId == invitation.ChildUserId, ct);

        if (alreadyLinked)
            return new InvitationRedeemResult(InvitationRedeemStatus.AlreadyLinked);

        invitation.AttemptCount++;
        invitation.UsedAt = DateTime.UtcNow;
        invitation.UsedByUserId = redeemerUserId;

        _context.ParentChildren.Add(new ParentChild
        {
            ParentId = redeemerUserId,
            ChildId = invitation.ChildUserId,
            IsPrimary = false
        });

        await _context.SaveChangesAsync(ct);
        return new InvitationRedeemResult(InvitationRedeemStatus.Success);
    }

    private async Task<bool> IsParentOfChildAsync(string parentId, string childId, CancellationToken ct)
    {
        return await _context.ParentChildren
            .AnyAsync(pc => pc.ParentId == parentId && pc.ChildId == childId, ct);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('='); // URL-safe Base64: replace +/= so the token is safe in query strings and email links
    }

    private static string GenerateManualCode()
    {
        var sb = new StringBuilder(8);
        var bytes = new byte[8];
        RandomNumberGenerator.Fill(bytes);
        for (int i = 0; i < 8; i++)
            sb.Append(ManualCodeAlphabet[bytes[i] % ManualCodeAlphabet.Length]);
        return sb.ToString();
    }

    private static string NormalizeCode(string code)
        => new string(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static string HashCode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
