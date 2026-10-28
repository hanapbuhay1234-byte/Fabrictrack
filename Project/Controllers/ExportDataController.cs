using System.Globalization;
using System.Security.Claims;
using System.Text;
using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FabriTrack.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class ExportDataController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExportDataController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            await PopulateFilterOptions();
            return View(new ExportDataViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> Preview(
            string dataType = "inventory",
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? categoryId = null,
            int? supplierId = null,
            bool includeArchived = false,
            bool includeVoided = false)
        {
            await PopulateFilterOptions(categoryId, supplierId);
            var model = new ExportDataViewModel
            {
                HasPreview = true,
                DataType = NormalizeDataType(dataType),
                StartDate = startDate?.Date,
                EndDate = endDate?.Date,
                CategoryId = categoryId,
                SupplierId = supplierId,
                IncludeArchived = includeArchived,
                IncludeVoided = includeVoided
            };

            if (startDate.HasValue && endDate.HasValue && startDate.Value.Date > endDate.Value.Date)
            {
                ModelState.AddModelError(string.Empty, "The end date must be on or after the start date.");
                return View("Index", model);
            }

            await BuildPreviewAsync(model);
            return View("Index", model);
        }

        [HttpGet]
        public async Task<IActionResult> Download(
            string dataType = "inventory",
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? categoryId = null,
            int? supplierId = null,
            bool includeArchived = false,
            bool includeVoided = false)
        {
            if (startDate.HasValue && endDate.HasValue && startDate.Value.Date > endDate.Value.Date)
            {
                TempData["Error"] = "The end date must be on or after the start date.";
                return RedirectToAction(nameof(Index));
            }

            var normalizedType = (dataType ?? "inventory").Trim().ToLowerInvariant();
            var content = normalizedType switch
            {
                "inventory" => await ExportInventory(categoryId, supplierId, includeArchived),
                "low-stock" => await ExportLowStock(categoryId, supplierId),
                "sales" => await ExportSales(startDate?.Date, endDate?.Date, includeVoided),
                "customers" => await ExportCustomers(),
                "suppliers" => await ExportSuppliers(),
                "categories" => await ExportCategories(),
                "users" => await ExportUsers(),
                "inventory-movements" => await ExportInventoryMovements(startDate?.Date, endDate?.Date),
                "audit-logs" => await ExportAuditLogs(startDate?.Date, endDate?.Date),
                "login-activity" => await ExportLoginActivity(startDate?.Date, endDate?.Date),
                _ => null
            };

            if (content == null)
                return BadRequest("Choose a valid export type.");

            await RecordExportAsync(normalizedType, startDate?.Date, endDate?.Date);

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content.Value.Csv)).ToArray(),
                "text/csv; charset=utf-8", $"{content.Value.FileName}-{timestamp}.csv");
        }

        private async Task PopulateFilterOptions(int? categoryId = null, int? supplierId = null)
        {
            ViewBag.Categories = new SelectList(await _context.Categories.OrderBy(c => c.Name).AsNoTracking().ToListAsync(), "CategoryId", "Name", categoryId);
            ViewBag.Suppliers = new SelectList(await _context.Suppliers.OrderBy(s => s.Name).AsNoTracking().ToListAsync(), "SupplierId", "Name", supplierId);
        }

        private static string NormalizeDataType(string? dataType) =>
            (dataType ?? "inventory").Trim().ToLowerInvariant();

        private async Task BuildPreviewAsync(ExportDataViewModel model)
        {
            const int previewLimit = 500;
            List<List<string>> rows;
            switch (model.DataType)
            {
                case "inventory":
                    model.DataTypeLabel = "Product inventory";
                    model.Columns = new() { "SKU", "Product", "Category", "Supplier", "Unit", "Price", "Stock", "Reorder level", "Status" };
                    var inventory = _context.Fabrics.AsNoTracking().Include(f => f.Category).Include(f => f.Supplier).AsQueryable();
                    if (!model.IncludeArchived) inventory = inventory.Where(f => f.IsActive);
                    if (model.CategoryId.HasValue) inventory = inventory.Where(f => f.CategoryId == model.CategoryId);
                    if (model.SupplierId.HasValue) inventory = inventory.Where(f => f.SupplierId == model.SupplierId);
                    model.TotalRows = await inventory.CountAsync();
                    var inventoryRows = await inventory.OrderBy(f => f.Name).Take(previewLimit).ToListAsync();
                    rows = inventoryRows.Select(f => new List<string> { f.Sku, f.Name, f.Category?.Name ?? "", f.Supplier?.Name ?? "", f.Unit,
                        f.PricePerUnit.ToString("0.00", CultureInfo.InvariantCulture), f.StockQuantity.ToString("0.##", CultureInfo.InvariantCulture),
                        f.ReorderLevel.ToString("0.##", CultureInfo.InvariantCulture), f.IsActive ? "Active" : "Archived" }).ToList();
                    break;
                case "low-stock":
                    model.DataTypeLabel = "Low-stock products";
                    model.Columns = new() { "SKU", "Product", "Supplier", "Unit", "On hand", "Reorder level", "Suggested quantity", "Status" };
                    var lowStock = _context.Fabrics.AsNoTracking().Include(f => f.Supplier)
                        .Where(f => f.IsActive && f.StockQuantity <= f.ReorderLevel);
                    if (model.CategoryId.HasValue) lowStock = lowStock.Where(f => f.CategoryId == model.CategoryId);
                    if (model.SupplierId.HasValue) lowStock = lowStock.Where(f => f.SupplierId == model.SupplierId);
                    model.TotalRows = await lowStock.CountAsync();
                    var lowRows = await lowStock.OrderBy(f => f.StockQuantity <= 0 ? 0 : 1).ThenBy(f => f.Name).Take(previewLimit).ToListAsync();
                    rows = lowRows.Select(f => new List<string> { f.Sku, f.Name, f.Supplier?.Name ?? "", f.Unit,
                        f.StockQuantity.ToString("0.##", CultureInfo.InvariantCulture),
                        f.ReorderLevel.ToString("0.##", CultureInfo.InvariantCulture),
                        Math.Max(f.ReorderLevel - f.StockQuantity, 0).ToString("0.##", CultureInfo.InvariantCulture),
                        f.StockQuantity <= 0 ? "Out of stock" : "Reorder" }).ToList();
                    break;
                case "sales":
                    model.DataTypeLabel = "Sales transactions";
                    model.Columns = new() { "Sale ID", "Date", "Customer", "Processed by", "Items", "Total (PHP)", "Status" };
                    var sales = _context.Sales.AsNoTracking().Include(s => s.Customer).Include(s => s.SaleItems).AsQueryable();
                    if (!model.IncludeVoided) sales = sales.Where(s => !s.IsVoided);
                    if (model.StartDate.HasValue) sales = sales.Where(s => s.SaleDate >= model.StartDate.Value);
                    if (model.EndDate.HasValue) sales = sales.Where(s => s.SaleDate < model.EndDate.Value.AddDays(1));
                    model.TotalRows = await sales.CountAsync();
                    var saleRows = await sales.OrderByDescending(s => s.SaleDate).Take(previewLimit).ToListAsync();
                    rows = saleRows.Select(s => new List<string> { s.SaleId.ToString(CultureInfo.InvariantCulture),
                        s.SaleDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), s.Customer?.FullName ?? "Walk-in",
                        s.CashierName ?? "Unknown", s.SaleItems.Count.ToString(CultureInfo.InvariantCulture),
                        s.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture), s.IsVoided ? "Voided" : "Completed" }).ToList();
                    break;
                case "customers":
                    model.DataTypeLabel = "Customers"; model.Columns = new() { "Customer ID", "Name", "Contact number", "Email", "Address" };
                    model.TotalRows = await _context.Customers.CountAsync();
                    var customers = await _context.Customers.AsNoTracking().OrderBy(c => c.FullName).Take(previewLimit).ToListAsync();
                    rows = customers.Select(c => new List<string> { c.CustomerId.ToString(), c.FullName, c.ContactNumber ?? "", c.Email ?? "", c.Address ?? "" }).ToList();
                    break;
                case "suppliers":
                    model.DataTypeLabel = "Suppliers"; model.Columns = new() { "Supplier ID", "Name", "Contact number", "Email", "Address" };
                    model.TotalRows = await _context.Suppliers.CountAsync();
                    var suppliers = await _context.Suppliers.AsNoTracking().OrderBy(s => s.Name).Take(previewLimit).ToListAsync();
                    rows = suppliers.Select(s => new List<string> { s.SupplierId.ToString(), s.Name, s.ContactNumber ?? "", s.Email ?? "", s.Address ?? "" }).ToList();
                    break;
                case "categories":
                    model.DataTypeLabel = "Categories"; model.Columns = new() { "Category ID", "Name", "Description" };
                    model.TotalRows = await _context.Categories.CountAsync();
                    var categories = await _context.Categories.AsNoTracking().OrderBy(c => c.Name).Take(previewLimit).ToListAsync();
                    rows = categories.Select(c => new List<string> { c.CategoryId.ToString(), c.Name, c.Description ?? "" }).ToList();
                    break;
                case "users":
                    model.DataTypeLabel = "User accounts"; model.Columns = new() { "Name", "Email", "Status", "Role", "Password change required" };
                    var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.FullName).Take(previewLimit).ToListAsync();
                    model.TotalRows = await _userManager.Users.CountAsync();
                    rows = new List<List<string>>();
                    foreach (var user in users)
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        rows.Add(new() { user.FullName, user.Email ?? user.UserName ?? "", user.IsActive ? "Active" : "Inactive", string.Join("; ", roles), user.RequirePasswordChange ? "Yes" : "No" });
                    }
                    break;
                case "inventory-movements":
                    model.DataTypeLabel = "Inventory transactions"; model.Columns = new() { "Date", "Product", "SKU", "Movement", "Quantity change" };
                    var movements = _context.InventoryMovements.AsNoTracking().Include(m => m.Fabric).AsQueryable();
                    if (model.StartDate.HasValue) movements = movements.Where(m => m.RecordedAt >= model.StartDate.Value);
                    if (model.EndDate.HasValue) movements = movements.Where(m => m.RecordedAt < model.EndDate.Value.AddDays(1));
                    model.TotalRows = await movements.CountAsync();
                    var movementRows = await movements.OrderByDescending(m => m.RecordedAt).Take(previewLimit).ToListAsync();
                    rows = movementRows.Select(m => new List<string> { m.RecordedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), m.Fabric?.Name ?? "", m.Fabric?.Sku ?? "", m.MovementType, m.QuantityChange.ToString("0.##", CultureInfo.InvariantCulture) }).ToList();
                    break;
                case "audit-logs":
                    model.DataTypeLabel = "Audit logs"; model.Columns = new() { "Date", "Actor", "Action", "Record type", "Record" };
                    var audit = _context.AuditLogs.AsNoTracking().AsQueryable();
                    if (model.StartDate.HasValue) audit = audit.Where(a => a.OccurredAt >= model.StartDate.Value);
                    if (model.EndDate.HasValue) audit = audit.Where(a => a.OccurredAt < model.EndDate.Value.AddDays(1));
                    model.TotalRows = await audit.CountAsync();
                    var auditRows = await audit.OrderByDescending(a => a.OccurredAt).Take(previewLimit).ToListAsync();
                    rows = auditRows.Select(a => new List<string> { a.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), a.ActorName, a.Action, a.EntityType, a.EntityName ?? "" }).ToList();
                    break;
                case "login-activity":
                    model.DataTypeLabel = "Login activity"; model.Columns = new() { "Date", "Event", "Account", "IP address" };
                    var activity = _context.LoginActivities.AsNoTracking().AsQueryable();
                    if (model.StartDate.HasValue) activity = activity.Where(a => a.OccurredAt >= model.StartDate.Value);
                    if (model.EndDate.HasValue) activity = activity.Where(a => a.OccurredAt < model.EndDate.Value.AddDays(1));
                    model.TotalRows = await activity.CountAsync();
                    var activityRows = await activity.OrderByDescending(a => a.OccurredAt).Take(previewLimit).ToListAsync();
                    rows = activityRows.Select(a => new List<string> { a.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), a.EventType, a.AccountName ?? a.AccountIdentifier ?? "Unrecognized account", a.IpAddress ?? "" }).ToList();
                    break;
                default:
                    model.DataTypeLabel = "Unknown data type"; model.Columns = new() { "Message" }; rows = new() { new() { "Choose a valid export type." } }; model.TotalRows = 1; break;
            }
            model.Rows = rows;
            model.PreviewIsLimited = model.TotalRows > rows.Count;
        }

        private async Task<(string Csv, string FileName)?> ExportInventory(int? categoryId, int? supplierId, bool includeArchived)
        {
            var query = _context.Fabrics.AsNoTracking().Include(f => f.Category).Include(f => f.Supplier).AsQueryable();
            if (!includeArchived) query = query.Where(f => f.IsActive);
            if (categoryId.HasValue) query = query.Where(f => f.CategoryId == categoryId.Value);
            if (supplierId.HasValue) query = query.Where(f => f.SupplierId == supplierId.Value);

            var rows = await query.OrderBy(f => f.Name).ThenBy(f => f.Color).ToListAsync();
            var csv = NewCsv("SKU,Product,Color,Pattern,Category,Supplier,Unit,Price per unit,Stock quantity,Reorder level,Status,Image URL");
            foreach (var row in rows)
            {
                AddRow(csv,
                    row.Sku, row.Name, row.Color, row.Pattern, row.Category?.Name, row.Supplier?.Name,
                    row.Unit, row.PricePerUnit.ToString("0.00", CultureInfo.InvariantCulture),
                    row.StockQuantity.ToString("0.##", CultureInfo.InvariantCulture),
                    row.ReorderLevel.ToString("0.##", CultureInfo.InvariantCulture),
                    row.IsActive ? "Active" : "Archived", row.ImageUrl);
            }
            return (csv.ToString(), "product-inventory");
        }

        private async Task<(string Csv, string FileName)?> ExportLowStock(int? categoryId, int? supplierId)
        {
            var query = _context.Fabrics.AsNoTracking().Include(f => f.Category).Include(f => f.Supplier)
                .Where(f => f.IsActive && f.StockQuantity <= f.ReorderLevel);
            if (categoryId.HasValue) query = query.Where(f => f.CategoryId == categoryId.Value);
            if (supplierId.HasValue) query = query.Where(f => f.SupplierId == supplierId.Value);

            var rows = await query.OrderBy(f => f.StockQuantity <= 0 ? 0 : 1)
                .ThenBy(f => f.Name).ToListAsync();
            var csv = NewCsv("SKU,Product,Category,Supplier,Unit,On hand,Reorder level,Suggested quantity,Status");
            foreach (var row in rows)
            {
                var suggested = Math.Max(row.ReorderLevel - row.StockQuantity, 0);
                AddRow(csv, row.Sku, row.Name, row.Category?.Name, row.Supplier?.Name, row.Unit,
                    row.StockQuantity.ToString("0.##", CultureInfo.InvariantCulture),
                    row.ReorderLevel > 0 ? row.ReorderLevel.ToString("0.##", CultureInfo.InvariantCulture) : "Not set",
                    suggested.ToString("0.##", CultureInfo.InvariantCulture), row.StockQuantity <= 0 ? "Out of stock" : "Reorder");
            }
            return (csv.ToString(), "low-stock-alerts");
        }

        private async Task<(string Csv, string FileName)?> ExportSales(DateTime? start, DateTime? end, bool includeVoided)
        {
            var query = _context.Sales.AsNoTracking().Include(s => s.Customer).Include(s => s.SaleItems).AsQueryable();
            if (!includeVoided) query = query.Where(s => !s.IsVoided);
            if (start.HasValue) query = query.Where(s => s.SaleDate >= start.Value);
            if (end.HasValue) query = query.Where(s => s.SaleDate < end.Value.AddDays(1));

            var rows = await query.OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.SaleId).ToListAsync();
            var csv = NewCsv("Sale ID,Date,Customer,Processed by,Item count,Total (PHP),Status,Void reason");
            foreach (var row in rows)
            {
                AddRow(csv, row.SaleId, row.SaleDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    row.Customer?.FullName ?? "Walk-in", row.CashierName ?? "Unknown", row.SaleItems.Count,
                    row.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture), row.IsVoided ? "Voided" : "Completed", row.VoidReason);
            }
            return (csv.ToString(), "sales-transactions");
        }

        private async Task<(string Csv, string FileName)?> ExportCustomers()
        {
            var rows = await _context.Customers.AsNoTracking().OrderBy(c => c.FullName).ToListAsync();
            var csv = NewCsv("Customer ID,Name,Contact number,Email,Address");
            foreach (var row in rows) AddRow(csv, row.CustomerId, row.FullName, row.ContactNumber, row.Email, row.Address);
            return (csv.ToString(), "customers");
        }

        private async Task<(string Csv, string FileName)?> ExportSuppliers()
        {
            var rows = await _context.Suppliers.AsNoTracking().Include(s => s.Fabrics).OrderBy(s => s.Name).ToListAsync();
            var csv = NewCsv("Supplier ID,Supplier name,Contact number,Email,Address,Active products,Archived products");
            foreach (var row in rows)
            {
                AddRow(csv, row.SupplierId, row.Name, row.ContactNumber, row.Email, row.Address,
                    row.Fabrics.Count(f => f.IsActive), row.Fabrics.Count(f => !f.IsActive));
            }
            return (csv.ToString(), "suppliers");
        }

        private async Task<(string Csv, string FileName)?> ExportCategories()
        {
            var rows = await _context.Categories.AsNoTracking().Include(c => c.Fabrics).OrderBy(c => c.Name).ToListAsync();
            var csv = NewCsv("Category ID,Category name,Description,Active products,Archived products");
            foreach (var row in rows)
                AddRow(csv, row.CategoryId, row.Name, row.Description, row.Fabrics.Count(f => f.IsActive), row.Fabrics.Count(f => !f.IsActive));
            return (csv.ToString(), "categories");
        }

        private async Task<(string Csv, string FileName)?> ExportUsers()
        {
            var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync();
            var csv = NewCsv("User ID,Name,Email,Status,Role,Password change required");
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                AddRow(csv, user.Id, user.FullName, user.Email ?? user.UserName, user.IsActive ? "Active" : "Inactive",
                    roles.Count == 0 ? "" : string.Join("; ", roles), user.RequirePasswordChange ? "Yes" : "No");
            }
            return (csv.ToString(), "users");
        }

        private async Task<(string Csv, string FileName)?> ExportInventoryMovements(DateTime? start, DateTime? end)
        {
            var query = _context.InventoryMovements.AsNoTracking().Include(m => m.Fabric).AsQueryable();
            if (start.HasValue) query = query.Where(m => m.RecordedAt >= start.Value);
            if (end.HasValue) query = query.Where(m => m.RecordedAt < end.Value.AddDays(1));
            var rows = await query.OrderByDescending(m => m.RecordedAt).ToListAsync();
            var csv = NewCsv("Date (UTC),Product,SKU,Movement,Quantity change,Unit,Notes");
            foreach (var row in rows)
                AddRow(csv, row.RecordedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    row.Fabric?.Name, row.Fabric?.Sku, row.MovementType,
                    row.QuantityChange.ToString("0.##", CultureInfo.InvariantCulture), row.Fabric?.Unit, row.Notes);
            return (csv.ToString(), "inventory-transactions");
        }

        private async Task<(string Csv, string FileName)?> ExportAuditLogs(DateTime? start, DateTime? end)
        {
            var query = _context.AuditLogs.AsNoTracking().AsQueryable();
            if (start.HasValue) query = query.Where(a => a.OccurredAt >= start.Value);
            if (end.HasValue) query = query.Where(a => a.OccurredAt < end.Value.AddDays(1));
            var rows = await query.OrderByDescending(a => a.OccurredAt).ToListAsync();
            var csv = NewCsv("Date (UTC),Actor,Action,Record type,Record,Record ID,Details");
            foreach (var row in rows)
                AddRow(csv, row.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), row.ActorName,
                    row.Action, row.EntityType, row.EntityName, row.EntityId, row.Details);
            return (csv.ToString(), "audit-log");
        }

        private async Task<(string Csv, string FileName)?> ExportLoginActivity(DateTime? start, DateTime? end)
        {
            var query = _context.LoginActivities.AsNoTracking().AsQueryable();
            if (start.HasValue) query = query.Where(a => a.OccurredAt >= start.Value);
            if (end.HasValue) query = query.Where(a => a.OccurredAt < end.Value.AddDays(1));
            var rows = await query.OrderByDescending(a => a.OccurredAt).ToListAsync();
            var csv = NewCsv("Date (UTC),Event,Account,IP address,Device");
            foreach (var row in rows)
                AddRow(csv, row.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), row.EventType,
                    row.AccountName ?? row.AccountIdentifier ?? "Unrecognized account", row.IpAddress, row.UserAgent);
            return (csv.ToString(), "login-activity");
        }

        private async Task RecordExportAsync(string dataType, DateTime? start, DateTime? end)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name ?? "Unknown user";
            _context.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId,
                ActorName = userName,
                Action = "Export",
                EntityType = "Data Export",
                EntityName = dataType,
                Details = start.HasValue || end.HasValue
                    ? $"Date range: {start?.ToString("yyyy-MM-dd") ?? "Any"} to {end?.ToString("yyyy-MM-dd") ?? "Any"}."
                    : "Full export.",
                OccurredAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        private static StringBuilder NewCsv(string header)
        {
            var csv = new StringBuilder();
            csv.AppendLine(header);
            return csv;
        }

        private static void AddRow(StringBuilder csv, params object?[] values)
        {
            for (var index = 0; index < values.Length; index++)
            {
                if (index > 0) csv.Append(',');
                csv.Append(CsvField(values[index]?.ToString() ?? string.Empty));
            }
            csv.AppendLine();
        }

        private static string CsvField(string value)
        {
            var trimmedValue = value.TrimStart();
            var safeValue = trimmedValue.Length > 0 && trimmedValue[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
                ? "'" + value
                : value;
            return $"\"{safeValue.Replace("\"", "\"\"")}\"";
        }
    }
}
