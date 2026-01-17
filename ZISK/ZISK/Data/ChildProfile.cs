using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data
{
    public class ChildProfile
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public DateOnly DateOfBirth { get; set; }

        public Guid? TeamId { get; set; }

        // TODO: Pridať FK na Team entitu keď bude vytvorená

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ParentChild> Parents { get; set; } = [];
        public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = [];
        public ICollection<AbsenceRequest> AbsenceRequests { get; set; } = [];
    }
}
