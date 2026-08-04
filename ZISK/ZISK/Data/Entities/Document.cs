using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data.Entities
{
    public enum DocumentCategory
    {
        General,
        Contract,
        TrainingPlan
    }

    public class Document : IDemoScoped
    {
        public Guid Id { get; set; }

        public Guid? DemoSessionId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string FilePath { get; set; } = string.Empty;

        public DocumentCategory Category { get; set; } = DocumentCategory.General;

        [MaxLength(450)]
        public string? TargetRoleId { get; set; }

        public string? UploadedByUserId { get; set; }

        [ForeignKey(nameof(UploadedByUserId))]
        public ApplicationUser? UploadedByUser { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
