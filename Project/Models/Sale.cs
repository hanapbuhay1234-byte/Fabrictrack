using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FabriTrack.Models
{
    public class Sale
    {
        public int SaleId { get; set; }

        public DateTime SaleDate { get; set; } = DateTime.Now;

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [StringLength(450)]
        public string? CashierId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Paid";

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }

    public class SaleItem
    {
        public int SaleItemId { get; set; }

        public int SaleId { get; set; }
        public Sale? Sale { get; set; }

        public int FabricId { get; set; }
        public Fabric? Fabric { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal => Quantity * UnitPrice;
    }
}
