using System.ComponentModel.DataAnnotations;

namespace ZISK.Data.Entities;

public class EmailConfirmationCode
{
    public const int MaxAttempts = 5;
    public const int LifetimeMinutes = 30;

    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(128)]
    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public int AttemptCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UsedAt { get; set; }
}
