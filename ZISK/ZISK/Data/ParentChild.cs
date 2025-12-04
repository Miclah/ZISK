using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data
{
    public class ParentChild
    {
        [MaxLength(20)]
        public string ParentId { get; set; } = string.Empty;

        [ForeignKey(nameof(ParentId))]
        public ApplicationUser Parent { get; set; } = null!;

        public int ChildProfileId { get; set; }

        [ForeignKey(nameof(ChildProfileId))]
        public ChildProfile ChildProfile { get; set; } = null!;

        [MaxLength(50)]
        public string? RelationshipType { get; set; }
    }
}
