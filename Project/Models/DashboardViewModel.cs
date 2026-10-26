namespace FabriTrack.Models
{
    // Holds the numbers and lists shown on the dashboard.
    public class DashboardViewModel
    {
        public bool IsAdmin { get; set; }
        public int PeriodDays { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal SalesRevenue { get; set; }
        public decimal PreviousSalesRevenue { get; set; }
        public int SalesCount { get; set; }
        public int PreviousSalesCount { get; set; }
        public decimal AverageSale { get; set; }
        public int OutOfStockCount { get; set; }
        public int LowStockCount { get; set; }
        public List<DashboardSalesDay> SalesTrend { get; set; } = new();
        public List<DashboardTopFabric> TopFabrics { get; set; } = new();
        public List<Fabric> StockAlerts { get; set; } = new();
        public List<Sale> RecentSales { get; set; } = new();
    }

    // Sales totals for one day on the dashboard chart.
    public class DashboardSalesDay
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public int BarHeightPercent { get; set; }
    }

    // Sales totals for one of the best-selling fabrics.
    public class DashboardTopFabric
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Color { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }
}
