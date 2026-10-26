using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FabriTrack.Models
{
    // Stores the main details for a sale, including its customer and cashier.
    public class Sale
    {
        public int SaleId { get; set; }

        public DateTime SaleDate { get; set; } = DateTime.Now;

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [StringLength(450)]
        public string? CashierId { get; set; }

        [StringLength(256)]
        // Keeps the cashier's name as it was when the sale was made.
        public string? CashierName { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Paid";

        // Voided sales stay in the history but are not counted in reports.
        public bool IsVoided { get; set; }

        public DateTime? VoidedAt { get; set; }

        [StringLength(450)]
        public string? VoidedByUserId { get; set; }

        [StringLength(200)]
        public string? VoidReason { get; set; }

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }

    // Stores one item on a sale, including its details at the time of purchase.
    public class SaleItem
    {
        public int SaleItemId { get; set; }

        public int SaleId { get; set; }
        public Sale? Sale { get; set; }

        public int FabricId { get; set; }
        public Fabric? Fabric { get; set; }

        [Required, StringLength(32)]
        public string ProductSku { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string ProductName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Color { get; set; }

        [Required, StringLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string Unit { get; set; } = ProductUnits.Yard;

        [Range(0.01, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0", "99999999.99")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        // Calculates this item's total from its quantity and price.
        public decimal Subtotal => Quantity * UnitPrice;
    }

    // Explains when and why a fabric's stock amount changed.
    public class InventoryMovement
    {
        public int InventoryMovementId { get; set; }

        public int FabricId { get; set; }
        public Fabric? Fabric { get; set; }

        public int? SaleId { get; set; }
        public Sale? Sale { get; set; }

        [Required, StringLength(30)]
        public string MovementType { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal QuantityChange { get; set; }

        [StringLength(250)]
        public string? Notes { get; set; }

        [StringLength(450)]
        public string? UserId { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.Now;
    }
}
