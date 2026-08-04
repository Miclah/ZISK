using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data.Entities
{
    public enum AbsenceRequestStatus
    {
        Received
    }

    public class AbsenceRequest : IDemoScoped
    {
        public Guid Id { get; set; }

        public Guid? DemoSessionId { get; set; }

        public string ChildId { get; set; } = string.Empty;

        [ForeignKey(nameof(ChildId))]
        public ApplicationUser Child { get; set; } = null!;

        public string ParentId { get; set; } = string.Empty;

        [ForeignKey(nameof(ParentId))]
        public ApplicationUser Parent { get; set; } = null!;

        public Guid? TrainingEventId { get; set; }

        [ForeignKey(nameof(TrainingEventId))]
        public TrainingEvent? TrainingEvent { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        [MaxLength(1000)]
        public string? Reason { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public AbsenceRequestStatus Status { get; set; } = AbsenceRequestStatus.Received;

        public string? ReviewedByUserId { get; set; }

        [ForeignKey(nameof(ReviewedByUserId))]
        public ApplicationUser? ReviewedByUser { get; set; }

        [MaxLength(500)]
        public string? ReviewNote { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
