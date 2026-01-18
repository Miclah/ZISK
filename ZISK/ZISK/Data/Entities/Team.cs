using System.ComponentModel.DataAnnotations;

namespace ZISK.Data.Entities
{
    public class Team
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? ShortName { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ChildProfile> Members { get; set; } = [];
        public ICollection<TrainingEvent> TrainingEvents { get; set; } = [];
    }
}
