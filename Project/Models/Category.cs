using System.ComponentModel.DataAnnotations;

namespace FabriTrack.Models
{
    // Groups fabrics into categories for the inventory and reports.
    public class Category
    {
        public int CategoryId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // The fabrics that belong to this category.
        public ICollection<Fabric> Fabrics { get; set; } = new List<Fabric>();
    }
}