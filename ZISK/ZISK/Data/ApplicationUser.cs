using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ZISK.Data
{ 
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ParentChild> Children { get; set; } = [];
    }
}
