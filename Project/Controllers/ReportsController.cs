using System.Globalization;
using System.Text;
using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FabriTrack.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context) => _context = context;

        // GET: /Reports?filterType=day|week|month|range&date=yyyy-MM-dd&start=yyyy-MM-dd&end=yyyy-MM-dd
        public async Task<IActionResult> Index(string? filterType, DateTime? date, DateTime? start, DateTime? end)
        {
            var model = new SalesReportViewModel
            {
                Date = date?.Date ?? DateTime.Today,
                Start = start?.Date,
                End = end?.Date
            };

            if (!TryGetPeriod(filterType, date, start, end, out var periodStart, out var periodEnd, out var normalizedFilter))
            {
                model.FilterType = normalizedFilter;
                model.PeriodLabel = "Select a valid reporting period";
                return View(model);
            }

            model.FilterType = normalizedFilter;
            model.PeriodStart = periodStart;
            model.PeriodEndExclusive = periodEnd;
            model.PeriodLabel = GetPeriodLabel(normalizedFilter, periodStart, periodEnd);
            model.HasValidPeriod = true;

            var previousPeriodLength = periodEnd - periodStart;
            var previousPeriodStart = periodStart - previousPeriodLength;
            var currentSales = _context.Sales
                .Where(sale => !sale.IsVoided && sale.SaleDate >= periodStart && sale.SaleDate < periodEnd);
            var previousSales = _context.Sales
                .Where(sale => !sale.IsVoided && sale.SaleDate >= previousPeriodStart && sale.SaleDate < periodStart);

            model.SalesRevenue = await currentSales.SumAsync(sale => (decimal?)sale.TotalAmount) ?? 0;
            model.PreviousSalesRevenue = await previousSales.SumAsync(sale => (decimal?)sale.TotalAmount) ?? 0;
            model.OrderCount = await currentSales.CountAsync();
            model.PreviousOrderCount = await previousSales.CountAsync();
            model.AverageOrderValue = model.OrderCount == 0 ? 0 : model.SalesRevenue / model.OrderCount;
            model.PreviousAverageOrderValue = model.PreviousOrderCount == 0
                ? 0
                : model.PreviousSalesRevenue / model.PreviousOrderCount;

            var dailyTrend = await currentSales
                .GroupBy(sale => sale.SaleDate.Date)
                .Select(group => new
                {
                    group.Key,
                    Revenue = group.Sum(sale => sale.TotalAmount),
                    OrderCount = group.Count()
                })
                .ToListAsync();
            model.Trend = BuildTrend(
                dailyTrend.Select(day => new SalesReportDailyAggregate
                {
                    Date = day.Key,
                    Revenue = day.Revenue,
                    OrderCount = day.OrderCount
                }),
                periodStart,
                periodEnd,
                out var trendGranularity);
            model.TrendGranularity = trendGranularity;

            model.CategoryBreakdown = await _context.SaleItems
                .Where(item => item.Sale != null && !item.Sale.IsVoided &&
                    item.Sale.SaleDate >= periodStart &&
                    item.Sale.SaleDate < periodEnd)
                .GroupBy(item => item.CategoryName)
                .Select(group => new SalesReportCategoryRow
                {
                    Name = group.Key,
                    OrderCount = group.Select(item => item.SaleId).Distinct().Count(),
                    Revenue = group.Sum(item => item.Quantity * item.UnitPrice)
                })
                .OrderByDescending(category => category.Revenue)
                .ToListAsync();

            model.FabricBreakdown = await _context.SaleItems
                .Where(item => item.Sale != null && !item.Sale.IsVoided &&
                    item.Sale.SaleDate >= periodStart &&
                    item.Sale.SaleDate < periodEnd)
                .GroupBy(item => new { item.ProductSku, item.ProductName, item.Color, item.Unit })
                .Select(group => new SalesReportFabricRow
                {
                    Sku = group.Key.ProductSku,
                    Name = group.Key.ProductName,
                    Color = group.Key.Color,
                    Unit = group.Key.Unit,
                    QuantitySold = group.Sum(item => item.Quantity),
                    Revenue = group.Sum(item => item.Quantity * item.UnitPrice),
                    OrderCount = group.Select(item => item.SaleId).Distinct().Count()
                })
                .OrderByDescending(fabric => fabric.Revenue)
                .ToListAsync();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Export(string? filterType, DateTime? date, DateTime? start, DateTime? end)
        {
            if (!TryGetPeriod(filterType, date, start, end, out var periodStart, out var periodEnd, out var normalizedFilter))
                return BadRequest(ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage));

            var previousPeriodLength = periodEnd - periodStart;
            var previousPeriodStart = periodStart - previousPeriodLength;
            var currentSales = _context.Sales
                .Where(sale => !sale.IsVoided && sale.SaleDate >= periodStart && sale.SaleDate < periodEnd);
            var previousSales = _context.Sales
                .Where(sale => !sale.IsVoided && sale.SaleDate >= previousPeriodStart && sale.SaleDate < periodStart);
            var salesRevenue = await currentSales.SumAsync(sale => (decimal?)sale.TotalAmount) ?? 0;
            var previousSalesRevenue = await previousSales.SumAsync(sale => (decimal?)sale.TotalAmount) ?? 0;
            var orderCount = await currentSales.CountAsync();
            var previousOrderCount = await previousSales.CountAsync();
            var averageOrderValue = orderCount == 0 ? 0 : salesRevenue / orderCount;
            var previousAverageOrderValue = previousOrderCount == 0 ? 0 : previousSalesRevenue / previousOrderCount;

            var categories = await _context.SaleItems
                .Where(item => item.Sale != null && !item.Sale.IsVoided &&
                    item.Sale.SaleDate >= periodStart &&
                    item.Sale.SaleDate < periodEnd)
                .GroupBy(item => item.CategoryName)
                .Select(group => new SalesReportCategoryRow
                {
                    Name = group.Key,
                    OrderCount = group.Select(item => item.SaleId).Distinct().Count(),
                    Revenue = group.Sum(item => item.Quantity * item.UnitPrice)
                })
                .OrderByDescending(category => category.Revenue)
                .ToListAsync();

            var fabrics = await _context.SaleItems
                .Where(item => item.Sale != null && !item.Sale.IsVoided &&
                    item.Sale.SaleDate >= periodStart &&
                    item.Sale.SaleDate < periodEnd)
                .GroupBy(item => new { item.ProductSku, item.ProductName, item.Color, item.Unit })
                .Select(group => new SalesReportFabricRow
                {
                    Sku = group.Key.ProductSku,
                    Name = group.Key.ProductName,
                    Color = group.Key.Color,
                    Unit = group.Key.Unit,
                    QuantitySold = group.Sum(item => item.Quantity),
                    Revenue = group.Sum(item => item.Quantity * item.UnitPrice),
                    OrderCount = group.Select(item => item.SaleId).Distinct().Count()
                })
                .OrderByDescending(fabric => fabric.Revenue)
                .ToListAsync();

            var dailyTrend = await currentSales
                .GroupBy(sale => sale.SaleDate.Date)
                .Select(group => new
                {
                    Date = group.Key,
                    Revenue = group.Sum(sale => sale.TotalAmount),
                    OrderCount = group.Count()
                })
                .ToListAsync();
            var trend = BuildTrend(
                dailyTrend.Select(day => new SalesReportDailyAggregate
                {
                    Date = day.Date,
                    Revenue = day.Revenue,
                    OrderCount = day.OrderCount
                }),
                periodStart,
                periodEnd,
                out var trendGranularity);

            var csv = new StringBuilder();
            csv.AppendLine("Sales Analytics");
            csv.AppendLine($"Period,{CsvField(GetPeriodLabel(normalizedFilter, periodStart, periodEnd))}");
            csv.AppendLine();
            csv.AppendLine("Metric,Selected period,Previous period");
            AppendCsvMetric(csv, "Sales revenue (PHP)", salesRevenue, previousSalesRevenue);
            AppendCsvMetric(csv, "Orders", orderCount, previousOrderCount);
            AppendCsvMetric(csv, "Average order value (PHP)", averageOrderValue, previousAverageOrderValue);
            csv.AppendLine();
            csv.AppendLine($"Revenue trend ({trendGranularity}),Start,End,Revenue (PHP),Orders");
            foreach (var point in trend)
            {
                csv.Append(CsvField(point.Label)).Append(',')
                    .Append(point.PeriodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                    .Append(point.PeriodEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                    .Append(point.Revenue.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                    .Append(point.OrderCount.ToString(CultureInfo.InvariantCulture)).AppendLine();
            }

            csv.AppendLine();
            csv.AppendLine("Sales by category");
            csv.AppendLine("Category,Orders,Sales amount (PHP)");
            foreach (var category in categories)
            {
                csv.Append(CsvField(category.Name)).Append(',')
                    .Append(category.OrderCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(category.Revenue.ToString("0.00", CultureInfo.InvariantCulture)).AppendLine();
            }

            csv.AppendLine();
            csv.AppendLine("Sales by product");
            csv.AppendLine("SKU,Product,Color,Quantity sold,Unit,Orders,Sales amount (PHP)");
            foreach (var fabric in fabrics)
            {
                csv.Append(CsvField(fabric.Sku)).Append(',')
                    .Append(CsvField(fabric.Name)).Append(',')
                    .Append(CsvField(fabric.Color)).Append(',')
                    .Append(fabric.QuantitySold.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(CsvField(fabric.Unit)).Append(',')
                    .Append(fabric.OrderCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(fabric.Revenue.ToString("0.00", CultureInfo.InvariantCulture)).AppendLine();
            }

            var filename = $"sales-analytics-{periodStart:yyyyMMdd}-{periodEnd.AddDays(-1):yyyyMMdd}.csv";
            var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(content, "text/csv; charset=utf-8", filename);
        }

        private bool TryGetPeriod(
            string? filterType,
            DateTime? date,
            DateTime? start,
            DateTime? end,
            out DateTime periodStart,
            out DateTime periodEnd,
            out string normalizedFilter)
        {
            normalizedFilter = string.IsNullOrWhiteSpace(filterType)
                ? "day"
                : filterType.Trim().ToLowerInvariant();
            periodStart = DateTime.Today;
            periodEnd = DateTime.Today.AddDays(1);

            if (!ModelState.IsValid)
                return false;

            var selectedDate = date?.Date ?? DateTime.Today;
            switch (normalizedFilter)
            {
                case "day":
                    periodStart = selectedDate;
                    periodEnd = periodStart.AddDays(1);
                    return true;

                case "week":
                    var daysSinceMonday = ((int)selectedDate.DayOfWeek + 6) % 7;
                    periodStart = selectedDate.AddDays(-daysSinceMonday);
                    periodEnd = periodStart.AddDays(7);
                    return true;

                case "month":
                    periodStart = new DateTime(selectedDate.Year, selectedDate.Month, 1);
                    periodEnd = periodStart.AddMonths(1);
                    return true;

                case "range":
                    if (!start.HasValue || !end.HasValue)
                    {
                        ModelState.AddModelError(string.Empty, "Choose both a start date and an end date for a custom range.");
                        return false;
                    }

                    periodStart = start.Value.Date;
                    periodEnd = end.Value.Date;
                    if (periodEnd < periodStart)
                    {
                        ModelState.AddModelError(string.Empty, "The end date must be the same as or later than the start date.");
                        return false;
                    }

                    periodEnd = periodEnd.AddDays(1);
                    return true;

                default:
                    ModelState.AddModelError(string.Empty, "Choose a valid report period.");
                    normalizedFilter = "day";
                    return false;
            }
        }

        private static List<SalesReportTrendPoint> BuildTrend(
            IEnumerable<SalesReportDailyAggregate> dailyTrend,
            DateTime periodStart,
            DateTime periodEnd,
            out string granularity)
        {
            var periodDays = (periodEnd - periodStart).Days;
            granularity = periodDays <= 31 ? "day" : periodDays <= 180 ? "week" : "month";
            var daily = dailyTrend.ToDictionary(day => day.Date.Date);
            var buckets = new List<SalesReportTrendPoint>();

            for (var bucketStart = periodStart.Date; bucketStart < periodEnd.Date;)
            {
                DateTime bucketEnd;
                if (granularity == "day")
                {
                    bucketEnd = bucketStart.AddDays(1);
                }
                else if (granularity == "week")
                {
                    var daysUntilMonday = (8 - (int)bucketStart.DayOfWeek) % 7;
                    bucketEnd = bucketStart.AddDays(daysUntilMonday == 0 ? 7 : daysUntilMonday);
                }
                else
                {
                    bucketEnd = new DateTime(bucketStart.Year, bucketStart.Month, 1).AddMonths(1);
                }

                if (bucketEnd > periodEnd)
                    bucketEnd = periodEnd;

                var bucket = new SalesReportTrendPoint
                {
                    PeriodStart = bucketStart,
                    PeriodEnd = bucketEnd.AddDays(-1),
                    Label = granularity switch
                    {
                        "day" => bucketStart.ToString("MMM d", CultureInfo.InvariantCulture),
                        "week" => $"{bucketStart:MMM d}–{bucketEnd.AddDays(-1):MMM d}",
                        _ => bucketStart.ToString("MMM yyyy", CultureInfo.InvariantCulture)
                    }
                };

                for (var date = bucketStart; date < bucketEnd; date = date.AddDays(1))
                {
                    if (!daily.TryGetValue(date, out var day))
                        continue;
                    bucket.Revenue += day.Revenue;
                    bucket.OrderCount += day.OrderCount;
                }

                buckets.Add(bucket);
                bucketStart = bucketEnd;
            }

            var maxRevenue = buckets.Count == 0 ? 0 : buckets.Max(bucket => bucket.Revenue);
            foreach (var bucket in buckets)
            {
                bucket.BarHeightPercent = maxRevenue == 0
                    ? 0
                    : Math.Max(4, (int)Math.Round(bucket.Revenue / maxRevenue * 100));
            }

            return buckets;
        }

        private static string GetPeriodLabel(string filterType, DateTime periodStart, DateTime periodEnd)
        {
            var lastIncludedDate = periodEnd.AddDays(-1);
            return filterType switch
            {
                "day" => periodStart.ToString("MMMM d, yyyy"),
                "week" => $"{periodStart:MMM d} – {lastIncludedDate:MMM d, yyyy} (Monday–Sunday)",
                "month" => periodStart.ToString("MMMM yyyy"),
                _ => $"{periodStart:MMM d, yyyy} – {lastIncludedDate:MMM d, yyyy}"
            };
        }

        private static string CsvField(string value)
        {
            var safeValue = value.TrimStart().StartsWith('=') ||
                value.TrimStart().StartsWith('+') ||
                value.TrimStart().StartsWith('-') ||
                value.TrimStart().StartsWith('@') ||
                value.StartsWith('\t') ||
                value.StartsWith('\r')
                ? "'" + value
                : value;
            return $"\"{safeValue.Replace("\"", "\"\"")}\"";
        }

        private static void AppendCsvMetric(StringBuilder csv, string name, decimal current, decimal previous)
        {
            csv.Append(CsvField(name)).Append(',')
                .Append(current.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(previous.ToString("0.00", CultureInfo.InvariantCulture)).AppendLine();
        }
    }
}
