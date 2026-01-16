using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ZISK.Data 
{
    public class ApplicationUser : IdentityUser
    {
        public UserProfile? UserProfile { get; set; }

        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        public bool IsActive { get; set; } = true;
    }

}
