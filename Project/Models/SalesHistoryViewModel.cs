using Microsoft.AspNetCore.Mvc.Rendering;

namespace FabriTrack.Models
{
    public class SalesHistoryViewModel
    {
        public string? Search { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? CustomerId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalRecords { get; set; }
        public int CompletedSales { get; set; }
        public int VoidedSales { get; set; }
        public decimal CompletedRevenue { get; set; }
        public List<SelectListItem> Customers { get; set; } = new();
        public List<SalesHistoryRowViewModel> Sales { get; set; } = new();

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }

    public class SalesHistoryRowViewModel
    {
        public int SaleId { get; set; }
        public DateTime SaleDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsVoided { get; set; }
    }
}
