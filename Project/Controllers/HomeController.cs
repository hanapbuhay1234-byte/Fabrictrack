using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

using System.Diagnostics;

namespace FabriTrack.Controllers
{
    // Loads the dashboard totals, sales chart, stock alerts, and recent sales.
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        public HomeController(ApplicationDbContext context) => _context = context;

        [Authorize(Roles = Roles.Admin + "," + Roles.Staff)]
        public async Task<IActionResult> Index(string? period)
        {
            // Use one of the available time periods for the dashboard.
            var periodDays = period switch
            {
                "30d" => 30,
                "90d" => 90,
                _ => 7
            };

            var today = DateTime.Today;
            var periodStart = today.AddDays(1 - periodDays);
            var periodEnd = today.AddDays(1);
            var previousPeriodStart = periodStart.AddDays(-periodDays);

            var currentSales = _context.Sales
                .Where(sale => !sale.IsVoided && sale.SaleDate >= periodStart && sale.SaleDate < periodEnd);
            var previousSales = _context.Sales
                .Where(sale => !sale.IsVoided && sale.SaleDate >= previousPeriodStart && sale.SaleDate < periodStart);
            var isAdmin = User.IsInRole(Roles.Admin);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!isAdmin)
            {
                // Staff see only sales they processed.
                currentSales = userId == null
                    ? currentSales.Where(sale => false)
                    : currentSales.Where(sale => sale.CashierId == userId);
                previousSales = userId == null
                    ? previousSales.Where(sale => false)
                    : previousSales.Where(sale => sale.CashierId == userId);
            }

            var model = new DashboardViewModel
            {
                IsAdmin = isAdmin,
                PeriodDays = periodDays,
                PeriodStart = periodStart,
                PeriodEnd = today,
                SalesRevenue = await currentSales.SumAsync(sale => (decimal?)sale.TotalAmount) ?? 0,
                PreviousSalesRevenue = await previousSales.SumAsync(sale => (decimal?)sale.TotalAmount) ?? 0,
                SalesCount = await currentSales.CountAsync(),
                PreviousSalesCount = await previousSales.CountAsync()
            };
            model.AverageSale = model.SalesCount == 0 ? 0 : model.SalesRevenue / model.SalesCount;

            var dailySales = await currentSales
                .GroupBy(sale => sale.SaleDate.Date)
                .Select(group => new
                {
                    Date = group.Key,
                    Revenue = group.Sum(sale => sale.TotalAmount),
                    OrderCount = group.Count()
                })
                .ToListAsync();

            var dailySalesByDate = dailySales.ToDictionary(sale => sale.Date.Date);
            var maxDailyRevenue = dailySales.Count == 0 ? 0 : dailySales.Max(sale => sale.Revenue);
            model.SalesTrend = Enumerable.Range(0, periodDays)
                .Select(offset =>
                {
                    var date = periodStart.AddDays(offset);
                    dailySalesByDate.TryGetValue(date, out var dailySale);
                    return new DashboardSalesDay
                    {
                        Date = date,
                        Revenue = dailySale?.Revenue ?? 0,
                        OrderCount = dailySale?.OrderCount ?? 0,
                        BarHeightPercent = maxDailyRevenue == 0
                            ? 0
                            : (int)Math.Round((dailySale?.Revenue ?? 0) / maxDailyRevenue * 100)
                    };
                })
                .ToList();

            model.TopFabrics = await _context.SaleItems
                .Where(item => item.Sale != null && !item.Sale.IsVoided &&
                    item.Sale.SaleDate >= periodStart &&
                    item.Sale.SaleDate < periodEnd &&
                    (isAdmin || (userId != null && item.Sale.CashierId == userId)))
                .GroupBy(item => new { item.ProductSku, item.ProductName, item.Color, item.Unit })
                .Select(group => new DashboardTopFabric
                {
                    Sku = group.Key.ProductSku,
                    Name = group.Key.ProductName,
                    Color = group.Key.Color,
                    Unit = group.Key.Unit,
                    QuantitySold = group.Sum(item => item.Quantity),
                    Revenue = group.Sum(item => item.Quantity * item.UnitPrice)
                })
                .OrderByDescending(fabric => fabric.Revenue)
                .ThenByDescending(fabric => fabric.QuantitySold)
                .Take(5)
                .ToListAsync();

            model.OutOfStockCount = await _context.Fabrics.CountAsync(fabric =>
                fabric.IsActive && fabric.StockQuantity <= 0);
            model.LowStockCount = await _context.Fabrics.CountAsync(fabric =>
                fabric.IsActive && fabric.StockQuantity > 0 && fabric.StockQuantity <= fabric.ReorderLevel);
            model.StockAlerts = await _context.Fabrics
                .Include(fabric => fabric.Category)
                .Where(fabric => fabric.IsActive && fabric.StockQuantity <= fabric.ReorderLevel)
                .OrderBy(fabric => fabric.StockQuantity <= 0 ? 0 : 1)
                .ThenBy(fabric => fabric.StockQuantity)
                .ThenBy(fabric => fabric.Name)
                .Take(6)
                .ToListAsync();

            var recentSales = _context.Sales
                .Where(sale => !sale.IsVoided)
                .AsQueryable();
            if (!isAdmin)
            {
                recentSales = userId == null
                    ? recentSales.Where(sale => false)
                    : recentSales.Where(sale => sale.CashierId == userId);
            }

            model.RecentSales = await recentSales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                .OrderByDescending(s => s.SaleDate)
                .Take(6)
                .ToListAsync();

            return View(model);
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}