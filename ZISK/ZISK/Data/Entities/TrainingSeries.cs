using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data.Entities;

public class TrainingSeries
{
    public Guid Id { get; set; }

    public Guid TeamId { get; set; }

    [ForeignKey(nameof(TeamId))]
    public Team Team { get; set; } = null!;

    public string CoachId { get; set; } = string.Empty;

    [ForeignKey(nameof(CoachId))]
    public ApplicationUser Coach { get; set; } = null!;

    public Guid SeasonId { get; set; }

    [ForeignKey(nameof(SeasonId))]
    public Season Season { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public int DaysOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    public TrainingType Type { get; set; } = TrainingType.Other;

    [MaxLength(1000)]
    public string? CoachNote { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TrainingEvent> Instances { get; set; } = [];
}
