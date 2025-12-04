using System.ComponentModel.DataAnnotations;

namespace ZISK.Data
{
    public class ChildProfile
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public DateTime DateOfBirth { get; set; }

        public bool IsActive { get; set; } = true;

    }
}
