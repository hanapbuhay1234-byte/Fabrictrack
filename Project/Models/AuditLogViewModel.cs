namespace FabriTrack.Models
{
    // Holds the audit search filters, page details, and results shown on screen.
    public class AuditLogViewModel
    {
        public string? Search { get; set; }
        public string? Actor { get; set; }
        public string? Action { get; set; }
        public string? EntityType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalRecords { get; set; }
        // Works out how many pages are needed for the results.
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
        public List<string> Actors { get; set; } = new();
        public List<string> Actions { get; set; } = new();
        public List<string> EntityTypes { get; set; } = new();
        public List<AuditLog> Entries { get; set; } = new();
    }
}
