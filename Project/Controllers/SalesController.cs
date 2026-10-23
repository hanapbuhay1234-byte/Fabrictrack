using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FabriTrack.Controllers
{
    [Authorize]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SalesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Receipt(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems).ThenInclude(si => si.Fabric)
                .FirstOrDefaultAsync(s => s.SaleId == id);
            if (sale == null) return NotFound();
            return View(sale);
        }

        public async Task<IActionResult> Index()
        {
            var sales = await _context.Sales.Include(s => s.Customer).Include(s => s.SaleItems)
                .OrderByDescending(s => s.SaleDate).ToListAsync();
            return View(sales);
        }

        public async Task<IActionResult> Details(int id)
        {
            var sale = await _context.Sales.Include(s => s.Customer)
                .Include(s => s.SaleItems).ThenInclude(si => si.Fabric)
                .FirstOrDefaultAsync(s => s.SaleId == id);
            if (sale == null) return NotFound();
            return View(sale);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Fabrics = await _context.Fabrics.Where(f => f.StockQuantity > 0).ToListAsync();
            ViewBag.Customers = new SelectList(await _context.Customers.ToListAsync(), "CustomerId", "FullName");
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int? customerId, List<int> fabricIds, List<decimal> quantities)
        {
            if (fabricIds == null || quantities == null || fabricIds.Count == 0 || fabricIds.Count != quantities.Count)
            {
                ModelState.AddModelError("", "Please add at least one valid fabric line item.");
                ViewBag.Fabrics = await _context.Fabrics.Where(f => f.StockQuantity > 0).ToListAsync();
                ViewBag.Customers = new SelectList(await _context.Customers.ToListAsync(), "CustomerId", "FullName");
                return View();
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sale = new Sale
                {
                    CustomerId = customerId,
                    CashierId = _userManager.GetUserId(User),
                    SaleDate = DateTime.Now
                };
                _context.Sales.Add(sale);

                decimal total = 0;
                for (int i = 0; i < fabricIds.Count; i++)
                {
                    if (quantities[i] <= 0) continue;

                    var fabric = await _context.Fabrics.FindAsync(fabricIds[i]);
                    if (fabric == null) continue;

                    if (fabric.StockQuantity < quantities[i])
                    {
                        ModelState.AddModelError("", $"Not enough stock for '{fabric.Name}'. Available: {fabric.StockQuantity} {fabric.Unit}.");
                        continue;
                    }

                    fabric.StockQuantity -= quantities[i];

                    var line = new SaleItem
                    {
                        Sale = sale,
                        FabricId = fabric.FabricId,
                        Quantity = quantities[i],
                        UnitPrice = fabric.PricePerUnit
                    };
                    sale.SaleItems.Add(line);
                    total += line.Subtotal;
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.Fabrics = await _context.Fabrics.Where(f => f.StockQuantity > 0).ToListAsync();
                    ViewBag.Customers = new SelectList(await _context.Customers.ToListAsync(), "CustomerId", "FullName");
                    return View();
                }

                sale.TotalAmount = total;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = $"Sale #{sale.SaleId} recorded — total ₱{total:N2}.";
                return RedirectToAction(nameof(Receipt), new { id = sale.SaleId });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}