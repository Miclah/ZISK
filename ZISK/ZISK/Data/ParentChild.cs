using System.ComponentModel.DataAnnotations.Schema;

namespace ZISK.Data
{
    public class ParentChild
    {
        public string ParentId { get; set; } = string.Empty;

        [ForeignKey(nameof(ParentId))]
        public ApplicationUser Parent { get; set; } = null!;

        public Guid ChildId { get; set; }

        [ForeignKey(nameof(ChildId))]
        public ChildProfile Child { get; set; } = null!;

        public bool IsPrimary { get; set; } = false;
    }
}
