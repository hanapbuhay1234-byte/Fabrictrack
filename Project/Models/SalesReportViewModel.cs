namespace FabriTrack.Models
{
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

    public class SalesReportTrendPoint
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public int BarHeightPercent { get; set; }
    }

    public class SalesReportDailyAggregate
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }

    public class SalesReportCategoryRow
    {
        public string Name { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
    }

    public class SalesReportFabricRow
    {
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }
}
