using System.ComponentModel.DataAnnotations;

namespace ZISK.Data.Entities;

public enum ParentInvitationType
{
    EmailLink,
    ManualCode
}

public class ParentInvitation : IDemoScoped
{
    public const int LifetimeHours = 24;
    public const int MaxAttempts = 5;
    public const int MaxActiveInvitations = 3;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? DemoSessionId { get; set; }

    [Required]
    [MaxLength(450)]
    public string ChildUserId { get; set; } = string.Empty;

    public ApplicationUser? Child { get; set; }

    [Required]
    [MaxLength(450)]
    public string InitiatorUserId { get; set; } = string.Empty;

    public ApplicationUser? Initiator { get; set; }

    public ParentInvitationType Type { get; set; }

    [MaxLength(256)]
    public string? TargetEmail { get; set; }

    [Required]
    [MaxLength(128)]
    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public int AttemptCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UsedAt { get; set; }

    [MaxLength(450)]
    public string? UsedByUserId { get; set; }

    public ApplicationUser? UsedBy { get; set; }
}
