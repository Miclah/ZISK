using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data
{
    public enum ExcuseStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public class Excuse
    {
        public Guid Id { get; set; }


        public Guid SubjectUserProfileId { get; set; }

        [ForeignKey(nameof(SubjectUserProfileId))]
        public UserProfile SubjectUserProfile { get; set; } = null!;

        public Guid ReporterUserProfileId { get; set; }

        [ForeignKey(nameof(ReporterUserProfileId))]
        public UserProfile ReporterUserProfile { get; set; } = null!;

        public Guid? TrainingSessionId { get; set; }

        // TODO: Pridať FK a navigáciu na TrainingSession, keď bude vytvorená

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        [MaxLength(1000)]
        public string? Reason { get; set; }

        public ExcuseStatus Status { get; set; } = ExcuseStatus.Pending;

        public Guid? ReviewedByUserProfileId { get; set; }

        [ForeignKey(nameof(ReviewedByUserProfileId))]
        public UserProfile? ReviewedByUserProfile { get; set; }

        [MaxLength(500)]
        public string? ReviewNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}