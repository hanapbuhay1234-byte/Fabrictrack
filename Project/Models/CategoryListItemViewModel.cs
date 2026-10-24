namespace FabriTrack.Models
{
    public class CategoryListItemViewModel
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ActiveProductCount { get; set; }
        public int ArchivedProductCount { get; set; }

        public int TotalProductCount => ActiveProductCount + ArchivedProductCount;
    }
}
