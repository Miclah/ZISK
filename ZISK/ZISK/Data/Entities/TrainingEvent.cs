using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data.Entities
{
    public enum TrainingType
    {
        Conditioning,
        Technical,
        Match,
        Recovery,
        Other
    }

    public class TrainingEvent
    {
        public Guid Id { get; set; }

        public Guid TeamId { get; set; }

        [ForeignKey(nameof(TeamId))]
        public Team Team { get; set; } = null!;

        public Guid? SeriesId { get; set; }

        [ForeignKey(nameof(SeriesId))]
        public TrainingSeries? Series { get; set; }

        public Guid SeasonId { get; set; }

        [ForeignKey(nameof(SeasonId))]
        public Season Season { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        [MaxLength(200)]
        public string? Location { get; set; }

        public TrainingType Type { get; set; } = TrainingType.Other;

        [MaxLength(1000)]
        public string? CoachNote { get; set; }

        public bool IsLocked { get; set; } = false;

        public bool IsCancelled { get; set; } = false;

        [MaxLength(500)]
        public string? CancelledReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = [];
        public ICollection<AbsenceRequest> AbsenceRequests { get; set; } = [];
    }
}
