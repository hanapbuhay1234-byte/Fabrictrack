using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text;

namespace FabriTrack.Controllers
{
    // Handles checkout, receipts, and sales history. Staff see only their own sales.
    [Authorize(Roles = Roles.Admin + "," + Roles.Staff)]
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
            var sale = await VisibleSales()
                .Include(s => s.Customer)
                .Include(s => s.SaleItems).ThenInclude(si => si.Fabric)
                .FirstOrDefaultAsync(s => s.SaleId == id);
            if (sale == null) return NotFound();
            var autoPrintReceiptId = TempData["AutoPrintReceiptId"] is int receiptId ? receiptId : (int?)null;
            ViewBag.AutoPrint = !sale.IsVoided && autoPrintReceiptId == id;
            return View(sale);
        }

        public async Task<IActionResult> Index(
            string? search,
            DateTime? startDate,
            DateTime? endDate,
            int? customerId,
            string? cashierId,
            int page = 1)
        {
            // Use the same search for the totals and the sales shown on the page.
            var model = new SalesHistoryViewModel
            {
                Search = search?.Trim(),
                StartDate = startDate?.Date,
                EndDate = endDate?.Date,
                CustomerId = customerId,
                CashierId = cashierId,
                Page = Math.Max(1, page)
            };
            await PopulateSalesHistoryCustomersAsync(model);
            if (User.IsInRole(Roles.Admin))
                await PopulateSalesHistoryCashiersAsync(model);

            if (startDate.HasValue && endDate.HasValue && startDate.Value.Date > endDate.Value.Date)
            {
                ModelState.AddModelError(nameof(model.EndDate), "The end date must be on or after the start date.");
                return View(model);
            }

            var query = FilterSalesHistory(
                VisibleSales().AsNoTracking(),
                model.Search,
                model.StartDate,
                model.EndDate,
                customerId,
                model.CashierId);
            var completedSales = query.Where(sale => !sale.IsVoided);
            model.TotalRecords = await query.CountAsync();
            model.CompletedSales = await completedSales.CountAsync();
            model.VoidedSales = await query.CountAsync(sale => sale.IsVoided);
            model.CompletedRevenue = await completedSales.SumAsync(sale => (decimal?)sale.TotalAmount) ?? 0m;

            model.Page = Math.Min(model.Page, model.TotalPages);
            model.Sales = await query
                .OrderByDescending(sale => sale.SaleDate)
                .ThenByDescending(sale => sale.SaleId)
                .Skip((model.Page - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(sale => new SalesHistoryRowViewModel
                {
                    SaleId = sale.SaleId,
                    SaleDate = sale.SaleDate,
                    CustomerName = sale.Customer == null ? "Walk-in" : sale.Customer.FullName,
                    CashierName = sale.CashierName ?? "Unknown",
                    ItemCount = sale.SaleItems.Count,
                    TotalAmount = sale.TotalAmount,
                    IsVoided = sale.IsVoided
                })
                .ToListAsync();

            return View(model);
        }

        public async Task<IActionResult> Export(
            string? search,
            DateTime? startDate,
            DateTime? endDate,
            int? customerId,
            string? cashierId)
        {
            if (startDate.HasValue && endDate.HasValue && startDate.Value.Date > endDate.Value.Date)
            {
                TempData["Error"] = "The end date must be on or after the start date.";
                return RedirectToAction(nameof(Index), new { search, startDate, endDate, customerId, cashierId });
            }

            var query = FilterSalesHistory(
                VisibleSales().AsNoTracking(),
                search?.Trim(),
                startDate?.Date,
                endDate?.Date,
                customerId,
                cashierId);
            var sales = await query
                .OrderByDescending(sale => sale.SaleDate)
                .ThenByDescending(sale => sale.SaleId)
                .Select(sale => new
                {
                    sale.SaleId,
                    sale.SaleDate,
                    CustomerName = sale.Customer == null ? "Walk-in" : sale.Customer.FullName,
                    CashierName = sale.CashierName ?? "Unknown",
                    ItemCount = sale.SaleItems.Count,
                    sale.TotalAmount,
                    Status = sale.IsVoided ? "Voided" : "Completed"
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Sale ID,Date,Customer,Processed by,Items,Total (PHP),Status");
            foreach (var sale in sales)
            {
                csv.Append(sale.SaleId.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(CsvField(sale.SaleDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(',')
                    .Append(CsvField(sale.CustomerName)).Append(',')
                    .Append(CsvField(sale.CashierName)).Append(',')
                    .Append(sale.ItemCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(sale.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                    .Append(CsvField(sale.Status)).AppendLine();
            }

            return File(
                new UTF8Encoding(true).GetBytes(csv.ToString()),
                "text/csv; charset=utf-8",
                $"sales-history-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Void(
            int id,
            string? reason,
            string? search,
            DateTime? startDate,
            DateTime? endDate,
            int? customerId,
            string? cashierId,
            int page = 1)
        {
            var trimmedReason = reason?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedReason) || trimmedReason.Length > 200)
            {
                TempData["Error"] = "Enter a reason for voiding the sale (up to 200 characters).";
                return RedirectToAction(nameof(Index), new { search, startDate, endDate, customerId, cashierId, page });
            }

            // Update the sale, restore stock, and record the reason as one all-or-nothing change.
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sale = await _context.Sales
                    .Include(item => item.SaleItems)
                    .ThenInclude(item => item.Fabric)
                    .FirstOrDefaultAsync(item => item.SaleId == id);
                if (sale == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                if (sale.IsVoided)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = $"Sale #{sale.SaleId} has already been voided.";
                    return RedirectToAction(nameof(Index), new { search, startDate, endDate, customerId, cashierId, page });
                }

                var itemsByFabric = sale.SaleItems
                    .GroupBy(saleItem => saleItem.FabricId)
                    .Select(group => new
                    {
                        Fabric = group.First().Fabric,
                        ProductName = group.First().ProductName,
                        Quantity = group.Sum(saleItem => saleItem.Quantity)
                    })
                    .ToList();
                foreach (var item in itemsByFabric)
                {
                    var fabric = item.Fabric;
                    if (fabric == null || fabric.StockQuantity > 99999999.99m - item.Quantity)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"Stock for '{item.ProductName}' cannot be restored within the allowed quantity range. No changes were made.";
                        return RedirectToAction(nameof(Index), new { search, startDate, endDate, customerId, cashierId, page });
                    }
                }

                sale.IsVoided = true;
                sale.PaymentStatus = "Voided";
                sale.VoidedAt = DateTime.Now;
                sale.VoidedByUserId = _userManager.GetUserId(User);
                sale.VoidReason = trimmedReason;

                foreach (var item in itemsByFabric)
                {
                    var fabric = item.Fabric!;
                    fabric.StockQuantity += item.Quantity;
                    _context.InventoryMovements.Add(new InventoryMovement
                    {
                        FabricId = fabric.FabricId,
                        SaleId = sale.SaleId,
                        MovementType = "Sale Void",
                        QuantityChange = item.Quantity,
                        Notes = $"Sale #{sale.SaleId} voided: {trimmedReason}",
                        UserId = sale.VoidedByUserId
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = $"Sale #{sale.SaleId} voided and inventory restored.";
                return RedirectToAction(nameof(Index), new { search, startDate, endDate, customerId, cashierId, page });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Sale or inventory changed while the void was being processed. Refresh Sales History and try again; no partial changes were saved.";
                return RedirectToAction(nameof(Index), new { search, startDate, endDate, customerId, cashierId, page });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var sale = await VisibleSales().Include(s => s.Customer)
                .Include(s => s.SaleItems).ThenInclude(si => si.Fabric)
                .FirstOrDefaultAsync(s => s.SaleId == id);
            if (sale == null) return NotFound();
            return View(nameof(Receipt), sale);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateCreateViewDataAsync();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustomer(QuickCustomerCreateViewModel input)
        {
            // Check the new customer's details and report problems without leaving checkout.
            input.FullName = input.FullName?.Trim() ?? string.Empty;
            input.ContactNumber = string.IsNullOrWhiteSpace(input.ContactNumber)
                ? null
                : input.ContactNumber.Trim();
            input.Email = string.IsNullOrWhiteSpace(input.Email)
                ? null
                : input.Email.Trim();

            ModelState.Clear();
            if (!TryValidateModel(input))
            {
                var errors = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .Distinct()
                    .ToList();
                return BadRequest(new { errors });
            }

            var customer = new Customer
            {
                FullName = input.FullName,
                ContactNumber = input.ContactNumber,
                Email = input.Email
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            return Json(new
            {
                customerId = customer.CustomerId,
                fullName = customer.FullName
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int? customerId, List<int> fabricIds, List<decimal> quantities)
        {
            // Add together repeated cart entries before checking each product's stock.
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

            if (customerId.HasValue &&
                !await _context.Customers.AnyAsync(customer => customer.CustomerId == customerId.Value))
            {
                ModelState.AddModelError("", "The selected customer is no longer available. Choose a customer again.");
            }

            if (!ModelState.IsValid)
            {
                return await RedisplayCreateFormAsync(fabricIds, quantities, customerId);
            }

            var itemQuantities = Enumerable.Range(0, fabricIds.Count)
                .GroupBy(index => fabricIds[index])
                .ToDictionary(group => group.Key, group => group.Sum(index => quantities[index]));

            var cashier = await _userManager.GetUserAsync(User);
            if (cashier == null)
                return Challenge();

            // Save the sale, its items, stock changes, and stock records together.
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sale = new Sale
                {
                    CustomerId = customerId,
                    CashierId = cashier.Id,
                    CashierName = cashier.FullName,
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
                TempData["AutoPrintReceiptId"] = sale.SaleId;
                return RedirectToAction(nameof(Receipt), new { id = sale.SaleId });
            }
            catch (CheckoutValidationException exception)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", exception.Message);
                return await RedisplayCreateFormAsync(fabricIds, quantities, customerId);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "Inventory changed while this order was being completed. No sale was recorded. Review the refreshed stock and try again.");
                return await RedisplayCreateFormAsync(fabricIds, quantities, customerId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static IQueryable<Sale> FilterSalesHistory(
            IQueryable<Sale> query,
            string? search,
            DateTime? startDate,
            DateTime? endDate,
            int? customerId,
            string? cashierId)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim();
                var hasSaleId = int.TryParse(searchTerm, out var saleId);
                query = query.Where(sale =>
                    (hasSaleId && sale.SaleId == saleId) ||
                    (sale.Customer != null && sale.Customer.FullName.Contains(searchTerm)) ||
                    (sale.CashierName != null && sale.CashierName.Contains(searchTerm)) ||
                    sale.SaleItems.Any(item =>
                        item.ProductName.Contains(searchTerm) ||
                        item.ProductSku.Contains(searchTerm)));
            }

            if (startDate.HasValue)
                query = query.Where(sale => sale.SaleDate >= startDate.Value.Date);

            if (endDate.HasValue)
            {
                var endExclusive = endDate.Value.Date.AddDays(1);
                query = query.Where(sale => sale.SaleDate < endExclusive);
            }

            if (customerId == 0)
                query = query.Where(sale => sale.CustomerId == null);
            else if (customerId.HasValue)
                query = query.Where(sale => sale.CustomerId == customerId.Value);

            if (!string.IsNullOrWhiteSpace(cashierId))
                query = query.Where(sale => sale.CashierId == cashierId);

            return query;
        }

        private IQueryable<Sale> VisibleSales()
        {
            var query = _context.Sales.AsQueryable();
            if (User.IsInRole(Roles.Admin))
                return query;

            // Staff can access only sales they processed.
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return userId == null
                ? query.Where(sale => false)
                : query.Where(sale => sale.CashierId == userId);
        }

        private async Task PopulateSalesHistoryCustomersAsync(SalesHistoryViewModel model)
        {
            model.Customers = await _context.Customers
                .AsNoTracking()
                .OrderBy(customer => customer.FullName)
                .Select(customer => new SelectListItem
                {
                    Value = customer.CustomerId.ToString(),
                    Text = customer.FullName,
                    Selected = model.CustomerId == customer.CustomerId
                })
                .ToListAsync();
        }

        private async Task PopulateSalesHistoryCashiersAsync(SalesHistoryViewModel model)
        {
            var cashierIds = await _context.Sales
                .AsNoTracking()
                .Where(sale => sale.CashierId != null)
                .Select(sale => sale.CashierId!)
                .Distinct()
                .ToListAsync();

            var cashiers = await _context.Users
                .AsNoTracking()
                .Where(user => cashierIds.Contains(user.Id))
                .OrderBy(user => user.FullName)
                .Select(user => new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.UserName
                })
                .ToListAsync();

            model.Cashiers = cashiers
                .Select(user => new SelectListItem
                {
                    Value = user.Id,
                    Text = string.IsNullOrWhiteSpace(user.FullName)
                        ? user.Email ?? user.UserName ?? "Unknown user"
                        : user.FullName,
                    Selected = model.CashierId == user.Id
                })
                .ToList();
        }

        private static string CsvField(string value)
        {
            // Format values safely for CSV files and spreadsheet apps.
            var trimmedValue = value.TrimStart();
            var safeValue = trimmedValue.StartsWith('=') ||
                trimmedValue.StartsWith('+') ||
                trimmedValue.StartsWith('-') ||
                trimmedValue.StartsWith('@') ||
                value.StartsWith('\t') ||
                value.StartsWith('\r')
                ? "'" + value
                : value;
            return $"\"{safeValue.Replace("\"", "\"\"")}\"";
        }

        private async Task<IActionResult> RedisplayCreateFormAsync(
            List<int> fabricIds,
            List<decimal> quantities,
            int? customerId)
        {
            ViewBag.InitialCart = System.Text.Json.JsonSerializer.Serialize(
                fabricIds.Zip(quantities, (fabricId, quantity) => new { fabricId, quantity }));
            await PopulateCreateViewDataAsync(customerId);
            return View(nameof(Create));
        }

        private async Task PopulateCreateViewDataAsync(int? customerId = null)
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
            var customers = await _context.Customers.OrderBy(customer => customer.FullName).ToListAsync();
            ViewBag.Customers = new SelectList(
                customers,
                "CustomerId",
                "FullName",
                customerId);
        }
    }
}