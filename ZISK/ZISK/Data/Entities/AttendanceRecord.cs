using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data.Entities
{
    public enum AttendanceStatus
    {
        Present,
        Absent,
        Excused,
        Late
    }

    public class AttendanceRecord
    {
        public Guid Id { get; set; }

        public Guid TrainingEventId { get; set; }

        // TODO: Pridať FK na TrainingEvent keď bude vytvorená

        public Guid ChildId { get; set; }

        [ForeignKey(nameof(ChildId))]
        public ChildProfile Child { get; set; } = null!;

        [Required]
        public AttendanceStatus Status { get; set; }

        public string? MarkedByUserId { get; set; }

        [ForeignKey(nameof(MarkedByUserId))]
        public ApplicationUser? MarkedByUser { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }
}