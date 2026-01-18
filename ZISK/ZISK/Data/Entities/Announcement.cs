using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data.Entities
{
    public enum AnnouncementPriority
    {
        Low,
        Medium,
        High
    }

    public enum TargetAudience
    {
        All,
        Parents,
        Athletes
    }

    public class Announcement
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;


        public Guid? TargetTeamId { get; set; }

        [ForeignKey(nameof(TargetTeamId))]
        public Team? TargetTeam { get; set; }

        public TargetAudience TargetAudience { get; set; } = TargetAudience.All;

        public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Medium;

        public bool IsPinned { get; set; } = false;

        public DateTime? ValidUntil { get; set; }

        public string AuthorUserId { get; set; } = string.Empty;

        [ForeignKey(nameof(AuthorUserId))]
        public ApplicationUser AuthorUser { get; set; } = null!;

        public DateTime PublishDate { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<AnnouncementAttachment> Attachments { get; set; } = [];
    }

    public class AnnouncementAttachment
    {
        public Guid Id { get; set; }

        public Guid AnnouncementId { get; set; }

        [ForeignKey(nameof(AnnouncementId))]
        public Announcement Announcement { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ContentType { get; set; }

        public long FileSize { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}