using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data.Entities
{
    public enum AttendanceStatus
    {
        Present,
        Absent,
        Excused
    }

    public class AttendanceRecord : IDemoScoped
    {
        public Guid Id { get; set; }

        public Guid? DemoSessionId { get; set; }

        public Guid TrainingEventId { get; set; }

        [ForeignKey(nameof(TrainingEventId))]
        public TrainingEvent TrainingEvent { get; set; } = null!;

        public string ChildId { get; set; } = string.Empty;

        [ForeignKey(nameof(ChildId))]
        public ApplicationUser Child { get; set; } = null!;

        [Required]
        public AttendanceStatus Status { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        [MaxLength(500)]
        public string? CoachComment { get; set; }

        public string? MarkedByUserId { get; set; }

        [ForeignKey(nameof(MarkedByUserId))]
        public ApplicationUser? MarkedByUser { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }
}
