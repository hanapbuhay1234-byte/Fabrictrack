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
        private sealed class CheckoutValidationException(string message) : Exception(message) { }

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
            await PopulateCreateViewDataAsync();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int? customerId, List<int> fabricIds, List<decimal> quantities)
        {
            if (fabricIds.Count == 0 || fabricIds.Count != quantities.Count)
            {
                ModelState.AddModelError("", "Please add at least one valid product line item.");
            }

            if (fabricIds.Count == quantities.Count)
            {
                for (var i = 0; i < quantities.Count; i++)
                {
                    if (quantities[i] <= 0 || decimal.Round(quantities[i], 2) != quantities[i])
                        ModelState.AddModelError("", "Each item quantity must be positive and use at most two decimal places.");
                }

                if (fabricIds.Count > 0 && quantities.All(quantity =>
                    quantity > 0 && decimal.Round(quantity, 2) == quantity))
                {
                    var requestedQuantities = Enumerable.Range(0, fabricIds.Count)
                        .GroupBy(index => fabricIds[index])
                        .ToDictionary(group => group.Key, group => group.Sum(index => quantities[index]));
                    var fabrics = await _context.Fabrics
                        .Include(fabric => fabric.Category)
                        .Where(fabric => fabric.IsActive && requestedQuantities.Keys.Contains(fabric.FabricId))
                        .ToDictionaryAsync(fabric => fabric.FabricId);

                    foreach (var (fabricId, requestedQuantity) in requestedQuantities)
                    {
                        if (!fabrics.TryGetValue(fabricId, out var fabric))
                        {
                            ModelState.AddModelError("", "One of the selected products is unavailable. Refresh the page and try again.");
                            continue;
                        }

                        if (ProductUnits.IsDiscrete(fabric.Unit) &&
                            requestedQuantity != decimal.Truncate(requestedQuantity))
                            ModelState.AddModelError("", $"'{fabric.Name}' is sold by whole {fabric.Unit} quantities.");

                        if (fabric.StockQuantity < requestedQuantity)
                            ModelState.AddModelError("", $"Not enough stock for '{fabric.Name}'. Available: {fabric.StockQuantity:0.##} {fabric.Unit}.");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.InitialCart = System.Text.Json.JsonSerializer.Serialize(
                    fabricIds.Zip(quantities, (fabricId, quantity) => new { fabricId, quantity }));
                await PopulateCreateViewDataAsync();
                return View();
            }

            var itemQuantities = Enumerable.Range(0, fabricIds.Count)
                .GroupBy(index => fabricIds[index])
                .ToDictionary(group => group.Key, group => group.Sum(index => quantities[index]));

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
                await _context.SaveChangesAsync();

                decimal total = 0;
                foreach (var (fabricId, quantity) in itemQuantities)
                {
                    var fabric = await _context.Fabrics
                        .Include(product => product.Category)
                        .FirstOrDefaultAsync(product => product.FabricId == fabricId);
                    if (fabric == null || !fabric.IsActive)
                    {
                        throw new CheckoutValidationException($"Product {fabricId} is no longer available. Please refresh the catalog.");
                    }

                    if (ProductUnits.IsDiscrete(fabric.Unit) && quantity != decimal.Truncate(quantity))
                        throw new CheckoutValidationException($"'{fabric.Name}' is sold by whole {fabric.Unit} quantities.");
                    if (fabric.StockQuantity < quantity)
                        throw new CheckoutValidationException($"Not enough stock for '{fabric.Name}'. Available: {fabric.StockQuantity:0.##} {fabric.Unit}.");

                    fabric.StockQuantity -= quantity;

                    var line = new SaleItem
                    {
                        Sale = sale,
                        FabricId = fabric.FabricId,
                        ProductSku = fabric.Sku,
                        ProductName = fabric.Name,
                        Color = fabric.Color,
                        CategoryName = fabric.Category?.Name ?? "Uncategorized",
                        Unit = fabric.Unit,
                        Quantity = quantity,
                        UnitPrice = fabric.PricePerUnit
                    };
                    sale.SaleItems.Add(line);
                    _context.InventoryMovements.Add(new InventoryMovement
                    {
                        FabricId = fabric.FabricId,
                        SaleId = sale.SaleId,
                        MovementType = "Sale",
                        QuantityChange = -quantity,
                        Notes = $"Sale #{sale.SaleId}",
                        UserId = sale.CashierId
                    });
                    total += line.Subtotal;
                }

                sale.TotalAmount = total;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = $"Sale #{sale.SaleId} recorded — total ₱{total:N2}.";
                return RedirectToAction(nameof(Receipt), new { id = sale.SaleId });
            }
            catch (CheckoutValidationException exception)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = exception.Message;
                return RedirectToAction(nameof(Create));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task PopulateCreateViewDataAsync()
        {
            ViewBag.Fabrics = await _context.Fabrics
                .Include(fabric => fabric.Category)
                .Where(fabric => fabric.IsActive)
                .OrderBy(fabric => fabric.Name)
                .ToListAsync();
            ViewBag.Categories = new SelectList(
                await _context.Categories.OrderBy(category => category.Name).ToListAsync(),
                "CategoryId",
                "Name");
            ViewBag.Customers = new SelectList(
                await _context.Customers.OrderBy(customer => customer.FullName).ToListAsync(),
                "CustomerId",
                "FullName");
        }
    }
}