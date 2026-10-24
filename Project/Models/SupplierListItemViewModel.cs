namespace FabriTrack.Models
{
    public class SupplierListItemViewModel
    {
        public int SupplierId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ContactNumber { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public int ActiveProductCount { get; set; }
        public int ArchivedProductCount { get; set; }

        public int TotalProductCount => ActiveProductCount + ArchivedProductCount;
    }
}
