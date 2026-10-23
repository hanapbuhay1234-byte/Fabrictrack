using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using System.Diagnostics;

namespace FabriTrack.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        public HomeController(ApplicationDbContext context) => _context = context;

        [Authorize]
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalFabrics = await _context.Fabrics.CountAsync();
            ViewBag.LowStockCount = await _context.Fabrics.CountAsync(f => f.StockQuantity <= 0);
            ViewBag.TotalCustomers = await _context.Customers.CountAsync();

            var today = DateTime.Today;
            ViewBag.TodaySales = await _context.Sales
                .Where(s => s.SaleDate.Date == today)
                .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

            ViewBag.RecentSales = await _context.Sales
                .Include(s => s.Customer)
                .OrderByDescending(s => s.SaleDate)
                .Take(5)
                .ToListAsync();

            return View();
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