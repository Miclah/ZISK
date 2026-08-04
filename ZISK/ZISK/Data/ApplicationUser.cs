using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using ZISK.Data.Entities;

namespace ZISK.Data
{
    public class ApplicationUser : IdentityUser, IDemoScoped
    {
        public Guid? DemoSessionId { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(20)]
        public override string? PhoneNumber { get; set; }

        [MaxLength(20)]
        public string? RodneCislo { get; set; }

        [MaxLength(300)]
        public string? Bydlisko { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ParentChild> Children { get; set; } = [];
        public ICollection<ParentChild> Parents { get; set; } = [];
        public ICollection<CoachTeam> CoachTeams { get; set; } = [];
        public ICollection<TeamMember> TeamMemberships { get; set; } = [];
    }
}
