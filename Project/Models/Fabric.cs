using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FabriTrack.Models
{
    public static class ProductUnits
    {
        public const string Yard = "yard";
        public const string Meter = "meter";
        public const string Roll = "roll";
        public const string Piece = "piece";

        public static bool IsValid(string? unit) =>
            unit is Yard or Meter or Roll or Piece;

        public static bool IsDiscrete(string? unit) =>
            unit is Roll or Piece;
    }

    public class Fabric : IValidatableObject
    {
        public int FabricId { get; set; }

        [Required, StringLength(32)]
        public string Sku { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Color { get; set; }

        [StringLength(50)]
        public string? Pattern { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0", "99999999.99")]
        public decimal PricePerUnit { get; set; }

        [StringLength(20)]
        public string Unit { get; set; } = ProductUnits.Yard;

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0", "99999999.99")]
        public decimal StockQuantity { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0", "99999999.99")]
        public decimal ReorderLevel { get; set; } = 5;

        public bool IsActive { get; set; } = true;

        public string? ImageUrl { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
        public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!ProductUnits.IsValid(Unit))
                yield return new ValidationResult("Choose yard, meter, roll, or piece.", new[] { nameof(Unit) });

            if (decimal.Round(PricePerUnit, 2) != PricePerUnit ||
                decimal.Round(StockQuantity, 2) != StockQuantity ||
                decimal.Round(ReorderLevel, 2) != ReorderLevel)
            {
                yield return new ValidationResult(
                    "Price, stock, and reorder values can have at most two decimal places.",
                    new[] { nameof(PricePerUnit), nameof(StockQuantity), nameof(ReorderLevel) });
            }

            if (ProductUnits.IsDiscrete(Unit) &&
                (StockQuantity != decimal.Truncate(StockQuantity) ||
                 ReorderLevel != decimal.Truncate(ReorderLevel)))
            {
                yield return new ValidationResult(
                    "Stock quantity and reorder level must be whole numbers for rolls and pieces.",
                    new[] { nameof(StockQuantity), nameof(ReorderLevel) });
            }
        }
    }
}