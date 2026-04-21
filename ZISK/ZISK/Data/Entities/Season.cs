using System.ComponentModel.DataAnnotations;

namespace ZISK.Data.Entities;

public class Season
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TrainingEvent> Trainings { get; set; } = [];
    public ICollection<TrainingSeries> Series { get; set; } = [];
}
