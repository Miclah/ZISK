using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data
{
    public class Announcement
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        /// Ak NULL = > globalny oznam inak pre urcitu skupinu
        public Guid? TargetGroupId { get; set; }

        // TODO: Pridať FK na Team

        public string AuthorUserId { get; set; } = string.Empty;

        [ForeignKey(nameof(AuthorUserId))]
        public ApplicationUser AuthorUser { get; set; } = null!;

        public DateTime PublishDate { get; set; } = DateTime.UtcNow;
    }
}