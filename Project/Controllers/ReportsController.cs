using FabriTrack.Data;
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
            var sales = _context.Sales.Include(s => s.Customer).Include(s => s.SaleItems).ThenInclude(si => si.Fabric).AsQueryable();

            DateTime today = DateTime.Today;
            if (string.IsNullOrEmpty(filterType) || filterType == "day")
            {
                var d = date?.Date ?? today;
                sales = sales.Where(s => s.SaleDate.Date == d);
            }
            else if (filterType == "week")
            {
                var d = date?.Date ?? today;
                var startOfWeek = d.AddDays(-(int)d.DayOfWeek);
                var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);
                sales = sales.Where(s => s.SaleDate >= startOfWeek && s.SaleDate <= endOfWeek);
            }
            else if (filterType == "month")
            {
                var d = date?.Date ?? today;
                var startOfMonth = new DateTime(d.Year, d.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);
                sales = sales.Where(s => s.SaleDate >= startOfMonth && s.SaleDate <= endOfMonth);
            }
            else if (filterType == "range")
            {
                if (start.HasValue && end.HasValue)
                {
                    var sDate = start.Value.Date;
                    var eDate = end.Value.Date.AddDays(1).AddTicks(-1);
                    sales = sales.Where(s => s.SaleDate >= sDate && s.SaleDate <= eDate);
                }
            }

            var list = await sales.OrderByDescending(s => s.SaleDate).ToListAsync();
            ViewBag.FilterType = filterType ?? "day";
            ViewBag.Date = date?.ToString("yyyy-MM-dd");
            ViewBag.Start = start?.ToString("yyyy-MM-dd");
            ViewBag.End = end?.ToString("yyyy-MM-dd");
            return View(list);
        }

        // Optional server-side helper to redirect to sales receipt
        public IActionResult Print(int id)
        {
            return RedirectToAction("Receipt", "Sales", new { id });
        }
    }
}
