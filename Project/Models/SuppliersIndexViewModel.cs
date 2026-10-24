namespace FabriTrack.Models
{
    public class SuppliersIndexViewModel
    {
        public List<SupplierListItemViewModel> Suppliers { get; set; } = new();
        public Supplier CreateSupplier { get; set; } = new();
        public Supplier EditSupplier { get; set; } = new();
        public bool OpenCreateModal { get; set; }
        public bool OpenEditModal { get; set; }

        public int ActiveProductCount => Suppliers.Sum(supplier => supplier.ActiveProductCount);
        public int ArchivedProductCount => Suppliers.Sum(supplier => supplier.ArchivedProductCount);
    }
}
