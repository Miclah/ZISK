using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data.Entities
{
    public enum AbsenceRequestStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public class AbsenceRequest
    {
        public Guid Id { get; set; }

        public Guid ChildId { get; set; }

        [ForeignKey(nameof(ChildId))]
        public ChildProfile Child { get; set; } = null!;

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

        public AbsenceRequestStatus Status { get; set; } = AbsenceRequestStatus.Pending;


        public string? ReviewedByUserId { get; set; }

        [ForeignKey(nameof(ReviewedByUserId))]
        public ApplicationUser? ReviewedByUser { get; set; }

        [MaxLength(500)]
        public string? ReviewNote { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}