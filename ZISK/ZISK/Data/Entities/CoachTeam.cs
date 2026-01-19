using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data.Entities
{
    public class CoachTeam
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string CoachId { get; set; } = string.Empty;

        [ForeignKey(nameof(CoachId))]
        public ApplicationUser Coach { get; set; } = null!;

        public Guid TeamId { get; set; }

        [ForeignKey(nameof(TeamId))]
        public Team Team { get; set; } = null!;

        public bool IsPrimary { get; set; } = false;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}