using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data
{

    public class UserProfile
    {
        public Guid Id { get; set; }

        public string? IdentityUserId { get; set; }

        [ForeignKey(nameof(IdentityUserId))]
        public ApplicationUser? IdentityUser { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        public DateOnly? DateOfBirth { get; set; }

        [MaxLength(500)]
        public string? ProfilePhotoUrl { get; set; }

        [MaxLength(256)]
        public string? ContactEmail { get; set; }

        public Guid? ParentId { get; set; }

        [ForeignKey(nameof(ParentId))]
        public UserProfile? Parent { get; set; }

        public ICollection<UserProfile> Children { get; set; } = [];

        public ICollection<ClubRole> ClubRoles { get; set; } = [];

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}