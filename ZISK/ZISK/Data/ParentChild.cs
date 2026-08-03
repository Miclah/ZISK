using System.ComponentModel.DataAnnotations.Schema;
using ZISK.Data.Entities;

namespace ZISK.Data
{
    public class ParentChild : IDemoScoped
    {
        public Guid? DemoSessionId { get; set; }

        public string ParentId { get; set; } = string.Empty;

        [ForeignKey(nameof(ParentId))]
        public ApplicationUser Parent { get; set; } = null!;

        public string ChildId { get; set; } = string.Empty;

        [ForeignKey(nameof(ChildId))]
        public ApplicationUser Child { get; set; } = null!;

        public bool IsPrimary { get; set; } = false;
    }
}
