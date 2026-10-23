using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FabriTrack.Models
{
    public class Fabric
    {
        public int FabricId { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Color { get; set; }

        [StringLength(50)]
        public string? Pattern { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(0, double.MaxValue)]
        public decimal PricePerUnit { get; set; }

        [StringLength(20)]
        public string Unit { get; set; } = "yard"; // yard or meter

        [Column(TypeName = "decimal(10,2)")]
        [Range(0, double.MaxValue)]
        public decimal StockQuantity { get; set; }

        public string? ImageUrl { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        // low stock concept removed; consumers should treat StockQuantity <= 0 as out of stock

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }
}