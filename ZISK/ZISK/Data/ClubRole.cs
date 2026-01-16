using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data
{
    public enum RoleType
    {
        Admin,
        Coach,
        Parent,
        Athlete
    }

    public class ClubRole
    {
        public Guid Id { get; set; }

        public Guid UserProfileId { get; set; }

        [ForeignKey(nameof(UserProfileId))]
        public UserProfile UserProfile { get; set; } = null!;

        [Required]
        public RoleType RoleType { get; set; }

        public Guid? ContextId { get; set; }
    }
}