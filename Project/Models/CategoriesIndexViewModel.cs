namespace FabriTrack.Models
{
    public class CategoriesIndexViewModel
    {
        public List<CategoryListItemViewModel> Categories { get; set; } = new();
        public Category CreateCategory { get; set; } = new();
        public Category EditCategory { get; set; } = new();
        public bool OpenCreateModal { get; set; }
        public bool OpenEditModal { get; set; }

        public int ActiveProductCount => Categories.Sum(category => category.ActiveProductCount);
        public int ArchivedProductCount => Categories.Sum(category => category.ArchivedProductCount);
    }
}
