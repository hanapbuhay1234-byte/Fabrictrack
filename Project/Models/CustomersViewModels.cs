using Microsoft.AspNetCore.Mvc.Rendering;

namespace FabriTrack.Models
{
    // Holds the customer list, search details, totals, and add/edit form state.
    public class CustomersIndexViewModel
    {
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCustomers { get; set; }
        public int CompletedPurchases { get; set; }
        public decimal CompletedSpend { get; set; }
        // Works out how many pages match the current search.
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCustomers / (double)PageSize));
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
        public List<CustomerListItemViewModel> Customers { get; set; } = new();
        public Customer CreateCustomer { get; set; } = new();
        public Customer EditCustomer { get; set; } = new();
        public bool OpenCreateModal { get; set; }
        public bool OpenEditModal { get; set; }
    }

    // Details and purchase totals shown for one customer in the list.
    public class CustomerListItemViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? ContactNumber { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public int CompletedPurchaseCount { get; set; }
        public decimal LifetimeSpend { get; set; }
    }

    // Customer details and one page of their completed purchases.
    public class CustomerDetailsViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? ContactNumber { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public int CompletedPurchaseCount { get; set; }
        public decimal LifetimeSpend { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPurchases { get; set; }
        public List<CustomerPurchaseViewModel> Purchases { get; set; } = new();
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalPurchases / (double)PageSize));
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }

    // The sale details shown for one purchase in a customer's history.
    public class CustomerPurchaseViewModel
    {
        public int SaleId { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
