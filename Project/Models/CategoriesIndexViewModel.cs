namespace FabriTrack.Models
{
    // Holds the category list and the information needed for its add and edit forms.
    public class CategoriesIndexViewModel
    {
        public List<CategoryListItemViewModel> Categories { get; set; } = new();
        public Category CreateCategory { get; set; } = new();
        public Category EditCategory { get; set; } = new();
        public bool OpenCreateModal { get; set; }
        public bool OpenEditModal { get; set; }
        public string? Search { get; set; }
        public string Sort { get; set; } = "date-desc";

        // Adds up the product counts for all categories in the list.
        public int ActiveProductCount => Categories.Sum(category => category.ActiveProductCount);
        public int ArchivedProductCount => Categories.Sum(category => category.ArchivedProductCount);
    }
}
