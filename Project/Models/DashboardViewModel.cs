namespace FabriTrack.Models
{
    public class DashboardViewModel
    {
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

    public class DashboardSalesDay
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public int BarHeightPercent { get; set; }
    }

    public class DashboardTopFabric
    {
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }
}
