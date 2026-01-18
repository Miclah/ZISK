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

        [ForeignKey(nameof(TrainingEventId))]
        public TrainingEvent TrainingEvent { get; set; } = null!;

        public Guid ChildId { get; set; }

        [ForeignKey(nameof(ChildId))]
        public ChildProfile Child { get; set; } = null!;

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