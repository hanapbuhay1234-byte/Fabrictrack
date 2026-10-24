using Microsoft.AspNetCore.Mvc.Rendering;

namespace FabriTrack.Models
{
    public class LoginActivityViewModel
    {
        public string? UserId { get; set; }
        public string? EventType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalRecords { get; set; }
        public List<SelectListItem> Users { get; set; } = new();
        public List<string> EventTypes { get; set; } = new();
        public List<LoginActivity> Entries { get; set; } = new();

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }
}
