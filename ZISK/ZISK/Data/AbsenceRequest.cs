using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data
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

        /// Ak NULL = > dlhodobá absencia
        public Guid? TrainingEventId { get; set; }

        // TODO: Pridať FK na TrainingEvent

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        [MaxLength(1000)]
        public string? Reason { get; set; }

        public AbsenceRequestStatus Status { get; set; } = AbsenceRequestStatus.Pending;

        public DateTime? ProcessedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}