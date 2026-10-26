namespace FabriTrack.Models
{
    // Holds the category details and product counts for one table row.
    public class CategoryListItemViewModel
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ActiveProductCount { get; set; }
        public int ArchivedProductCount { get; set; }

        // Adds the active and archived product counts together.
        public int TotalProductCount => ActiveProductCount + ArchivedProductCount;
    }
}
