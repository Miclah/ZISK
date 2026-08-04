using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;
using ZISK.Shared.DTOs.Invitations;
using ZISK.Shared.Localization;

namespace ZISK.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class InvitationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IParentInvitationService _invitationService;
    private readonly SmtpEmailSender _emailSender;
    private readonly IConfiguration _config;
    private readonly ILogger<InvitationsController> _logger;
    private readonly ICurrentLanguage _currentLanguage;

    public InvitationsController(
        ApplicationDbContext context,
        IParentInvitationService invitationService,
        SmtpEmailSender emailSender,
        IConfiguration config,
        ILogger<InvitationsController> logger,
        ICurrentLanguage currentLanguage)
    {
        _context = context;
        _invitationService = invitationService;
        _emailSender = emailSender;
        _config = config;
        _logger = logger;
        _currentLanguage = currentLanguage;
    }

    [HttpPost("children/{childId}/invitations/email")]
    [Authorize(Roles = "Parent,Admin")]
    public async Task<IActionResult> SendEmailInvitation(string childId, [FromBody] SendEmailInvitationRequest request)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(callerId))
            return Unauthorized();

        var result = await _invitationService.IssueEmailLinkAsync(childId, callerId, request.TargetEmail);

        if (result.Status == InvitationIssueStatus.Forbidden)
            return Forbid();
        if (result.Status == InvitationIssueStatus.TooManyActive)
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.invitation.tooManyActive"));
        if (result.Invitation is null || result.PlainCode is null)
            return StatusCode(500);

        var child = await _context.Users.FindAsync(childId);
        var initiator = await _context.Users.FindAsync(callerId);
        var childName = child != null ? $"{child.FirstName} {child.LastName}" : childId;

        var baseUrl = _config["ApiBaseAddress"] ?? "http://localhost:5224";

        // Two different flows: existing users go directly to the accept page with the token;
        // new users land on the registration page where the invite code pre-fills their form and links the child after signup.
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.TargetEmail);
        string acceptUrl;
        if (existingUser != null)
            acceptUrl = $"{baseUrl}/pozvanka/prijat?token={Uri.EscapeDataString(result.PlainCode)}";
        else
            acceptUrl = $"{baseUrl}/registracia?email={Uri.EscapeDataString(request.TargetEmail)}&invite={Uri.EscapeDataString(result.PlainCode)}";

        if (initiator != null)
        {
            try { await _emailSender.SendParentInvitationLinkAsync(request.TargetEmail, initiator, childName, acceptUrl); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to send invitation email to {Email}", request.TargetEmail); }
        }

        return Accepted();
    }

    [HttpPost("children/{childId}/invitations/code")]
    [Authorize(Roles = "Parent,Admin")]
    public async Task<ActionResult<InvitationCodeResponse>> GenerateCode(string childId)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(callerId))
            return Unauthorized();

        var result = await _invitationService.IssueManualCodeAsync(childId, callerId);

        if (result.Status == InvitationIssueStatus.Forbidden)
            return Forbid();
        if (result.Status == InvitationIssueStatus.TooManyActive)
            return BadRequest(Translations.Get(_currentLanguage.Current, "errors.invitation.tooManyActive"));
        if (result.Invitation is null || result.PlainCode is null)
            return StatusCode(500);

        return Ok(new InvitationCodeResponse(result.PlainCode, result.Invitation.ExpiresAt));
    }

    [HttpGet("invitations/pending")]
    public async Task<ActionResult<List<PendingInvitationDto>>> GetPending()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var now = DateTime.UtcNow;
        // Only EmailLink invitations appear in this list — ManualCode invitations are redeemed by entering a code, not via this endpoint.
        // Both temporal conditions (UsedAt == null AND ExpiresAt > now) are required to show truly open invitations.
        var invitations = await _context.ParentInvitations
            .Include(pi => pi.Child)
            .Include(pi => pi.Initiator)
            .Where(pi => pi.Type == ParentInvitationType.EmailLink
                         && pi.TargetEmail == userEmail
                         && pi.UsedAt == null
                         && pi.ExpiresAt > now)
            .OrderByDescending(pi => pi.CreatedAt)
            .ToListAsync();

        
        var result = invitations.Select(pi => new PendingInvitationDto(
            pi.Id,
            pi.ChildUserId,
            pi.Child != null ? $"{pi.Child.FirstName} {pi.Child.LastName}" : pi.ChildUserId,
            pi.Initiator != null ? $"{pi.Initiator.FirstName} {pi.Initiator.LastName}" : pi.InitiatorUserId,
            pi.ExpiresAt
        )).ToList();

        return Ok(result);
    }

    [HttpPost("invitations/accept")]
    public async Task<IActionResult> Accept([FromQuery] string token)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _invitationService.RedeemEmailLinkAsync(token, userId);
        return result.Status switch
        {
            InvitationRedeemStatus.Success => Ok(),
            InvitationRedeemStatus.AlreadyLinked => BadRequest(Translations.Get(_currentLanguage.Current, "errors.invitation.alreadyLinked")),
            InvitationRedeemStatus.AlreadyUsed => BadRequest(Translations.Get(_currentLanguage.Current, "errors.invitation.alreadyUsed")),
            InvitationRedeemStatus.Expired => BadRequest(Translations.Get(_currentLanguage.Current, "errors.invitation.expired")),
            InvitationRedeemStatus.TooManyAttempts => BadRequest(Translations.Get(_currentLanguage.Current, "errors.invitation.tooManyAttempts")),
            _ => BadRequest(Translations.Get(_currentLanguage.Current, "errors.invitation.notFound"))
        };
    }

    [HttpPost("invitations/redeem-code")]
    public async Task<IActionResult> RedeemCode([FromBody] RedeemCodeRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _invitationService.RedeemManualCodeAsync(request.Code, userId);
        return result.Status switch
        {
            InvitationRedeemStatus.Success => Ok(),
            InvitationRedeemStatus.AlreadyLinked => BadRequest(Translations.Get(_currentLanguage.Current, "errors.invitation.alreadyLinked")),
            InvitationRedeemStatus.AlreadyUsed => BadRequest(Translations.Get(_currentLanguage.Current, "errors.invitation.alreadyUsed")),
            InvitationRedeemStatus.Expired => BadRequest(Translations.Get(_currentLanguage.Current, "errors.invitation.codeExpired")),
            InvitationRedeemStatus.TooManyAttempts => BadRequest(Translations.Get(_currentLanguage.Current, "errors.invitation.tooManyWrongAttempts")),
            _ => BadRequest(Translations.Get(_currentLanguage.Current, "errors.invitation.wrongCode"))
        };
    }
}
