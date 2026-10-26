namespace FabriTrack.Models
{
    // Holds the report totals and grouped sales data for the selected dates.
    public class SalesReportViewModel
    {
        public string FilterType { get; set; } = "day";
        public DateTime Date { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEndExclusive { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public string TrendGranularity { get; set; } = "day";
        public bool HasValidPeriod { get; set; }
        public decimal SalesRevenue { get; set; }
        public decimal PreviousSalesRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal PreviousAverageOrderValue { get; set; }
        public int OrderCount { get; set; }
        public int PreviousOrderCount { get; set; }
        public List<SalesReportTrendPoint> Trend { get; set; } = new();
        public List<SalesReportCategoryRow> CategoryBreakdown { get; set; } = new();
        public List<SalesReportFabricRow> FabricBreakdown { get; set; } = new();
    }

    // Sales totals for one day, week, or month in the report.
    public class SalesReportTrendPoint
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public int BarHeightPercent { get; set; }
    }

    // Sales totals for one day, used to build the report chart.
    public class SalesReportDailyAggregate
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }

    // Sales totals grouped by the category saved on each sale.
    public class SalesReportCategoryRow
    {
        public string Name { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
    }

    // Sales totals and quantities grouped by the product details saved on each sale.
    public class SalesReportFabricRow
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Color { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }
}
