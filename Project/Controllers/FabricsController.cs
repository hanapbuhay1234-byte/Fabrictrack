using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FabriTrack.Controllers
{
    [Authorize]
    public class FabricsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        public FabricsController(ApplicationDbContext context, IWebHostEnvironment env) { _context = context; _env = env; }

        public async Task<IActionResult> Index(
            string? search,
            int? categoryId,
            int? supplierId,
            string? unit,
            string? stockStatus,
            string? sort,
            bool includeArchived = false)
        {
            var fabrics = _context.Fabrics
                .Include(f => f.Category)
                .Include(f => f.Supplier)
                .AsQueryable();

            if (includeArchived && User.IsInRole(Roles.Admin))
                fabrics = fabrics.Where(f => !f.IsActive);
            else
                fabrics = fabrics.Where(f => f.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
                fabrics = fabrics.Where(f =>
                    f.Sku.Contains(search) ||
                    f.Name.Contains(search) ||
                    (f.Color != null && f.Color.Contains(search)) ||
                    (f.Pattern != null && f.Pattern.Contains(search)));

            if (categoryId.HasValue)
                fabrics = fabrics.Where(f => f.CategoryId == categoryId);

            if (supplierId.HasValue)
                fabrics = fabrics.Where(f => f.SupplierId == supplierId);

            if (ProductUnits.IsValid(unit))
                fabrics = fabrics.Where(f => f.Unit == unit);

            if (stockStatus == "out")
                fabrics = fabrics.Where(f => f.StockQuantity <= 0);
            else if (stockStatus == "low")
                fabrics = fabrics.Where(f => f.StockQuantity > 0 && f.StockQuantity <= f.ReorderLevel);
            else if (stockStatus == "in")
                fabrics = fabrics.Where(f => f.StockQuantity > f.ReorderLevel);

            fabrics = sort switch
            {
                "name-desc" => fabrics.OrderByDescending(f => f.Name).ThenBy(f => f.Color),
                "stock" => fabrics.OrderBy(f => f.StockQuantity).ThenBy(f => f.Name),
                "stock-desc" => fabrics.OrderByDescending(f => f.StockQuantity).ThenBy(f => f.Name),
                "price" => fabrics.OrderBy(f => f.PricePerUnit).ThenBy(f => f.Name),
                "price-desc" => fabrics.OrderByDescending(f => f.PricePerUnit).ThenBy(f => f.Name),
                _ => fabrics.OrderBy(f => f.Name).ThenBy(f => f.Color)
            };

            ViewBag.Categories = new SelectList(await _context.Categories.OrderBy(c => c.Name).ToListAsync(), "CategoryId", "Name", categoryId);
            ViewBag.Suppliers = new SelectList(await _context.Suppliers.OrderBy(s => s.Name).ToListAsync(), "SupplierId", "Name", supplierId);
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.SupplierId = supplierId;
            ViewBag.Unit = unit;
            ViewBag.StockStatus = stockStatus;
            ViewBag.Sort = sort ?? "name";
            ViewBag.IncludeArchived = includeArchived && User.IsInRole(Roles.Admin);
            ViewBag.ActiveProductCount = await _context.Fabrics.CountAsync(fabric => fabric.IsActive);
            ViewBag.ArchivedProductCount = User.IsInRole(Roles.Admin)
                ? await _context.Fabrics.CountAsync(fabric => !fabric.IsActive)
                : 0;

            return View(await fabrics.ToListAsync());
        }

        public async Task<IActionResult> Details(int id, string? returnTo)
        {
            var fabric = await _context.Fabrics
                .Include(f => f.Category)
                .Include(f => f.Supplier)
                .Include(f => f.InventoryMovements.OrderByDescending(movement => movement.RecordedAt).Take(20))
                .FirstOrDefaultAsync(f => f.FabricId == id);
            if (fabric == null) return NotFound();
            if (!fabric.IsActive && !User.IsInRole(Roles.Admin)) return NotFound();
            ViewBag.ReturnToStockAlerts = returnTo == nameof(LowStock);
            return View(fabric);
        }

        [Authorize(Roles = Roles.Admin)]
        public IActionResult Create()
        {
            ViewBag.Units = GetUnits();
            PopulateDropdowns();
            return View(new Fabric());
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Create(Fabric fabric)
        {
            fabric.IsActive = true;
            if (string.IsNullOrWhiteSpace(fabric.Sku))
            {
                fabric.Sku = $"FT-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
                ModelState.Remove(nameof(fabric.Sku));
            }
            else
            {
                fabric.Sku = fabric.Sku.Trim().ToUpperInvariant();
            }
            var file = Request.Form.Files.FirstOrDefault();
            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            const long maxBytes = 3 * 1024 * 1024; // 3 MB
            if (file != null)
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExt.Contains(ext))
                {
                    ModelState.AddModelError("ImageUrl", "Only JPG, PNG and WEBP images are allowed.");
                }
                else if (file.Length > maxBytes)
                {
                    ModelState.AddModelError("ImageUrl", "Image must be 3 MB or smaller.");
                }
            }

            if (await _context.Fabrics.AnyAsync(existing => existing.Sku == fabric.Sku))
                ModelState.AddModelError(nameof(fabric.Sku), "This SKU is already in use.");

            if (ModelState.IsValid)
            {
                string? newImagePath = null;
                if (file != null)
                {
                    var uploads = Path.Combine(_env.WebRootPath, "uploads", "fabrics");
                    Directory.CreateDirectory(uploads);
                    var filename = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName).ToLowerInvariant();
                    var filepath = Path.Combine(uploads, filename);
                    using var stream = System.IO.File.Create(filepath);
                    await file.CopyToAsync(stream);
                    newImagePath = $"/uploads/fabrics/{filename}";
                    fabric.ImageUrl = newImagePath; // set before save
                }

                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Add(fabric);
                    await _context.SaveChangesAsync();
                    if (fabric.StockQuantity > 0)
                    {
                        _context.InventoryMovements.Add(new InventoryMovement
                        {
                            FabricId = fabric.FabricId,
                            MovementType = "Opening stock",
                            QuantityChange = fabric.StockQuantity,
                            Notes = "Initial inventory balance",
                            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        });
                        await _context.SaveChangesAsync();
                    }
                    await transaction.CommitAsync();
                    TempData["Success"] = $"'{fabric.Name}' added to inventory.";
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    // If DB save failed and we wrote a new file, remove the orphan file
                    if (!string.IsNullOrEmpty(newImagePath))
                    {
                        try
                        {
                            var webRoot = _env.WebRootPath;
                            var uploadsDir = Path.GetFullPath(Path.Combine(webRoot, "uploads", "fabrics")) + Path.DirectorySeparatorChar;
                            var candidate = Path.GetFullPath(Path.Combine(webRoot, newImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                            if (candidate.StartsWith(uploadsDir, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(candidate))
                            {
                                System.IO.File.Delete(candidate);
                            }
                        }
                        catch { }
                    }
                    throw;
                }
            }
            ViewBag.Units = GetUnits();
            PopulateDropdowns(fabric);
            return View(fabric);
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Edit(int id)
        {
            var fabric = await _context.Fabrics.FindAsync(id);
            if (fabric == null) return NotFound();
            ViewBag.Units = GetUnits();
            PopulateDropdowns(fabric);
            return View(fabric);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Edit(int id, Fabric fabric)
        {
            if (id != fabric.FabricId) return NotFound();

            var existing = await _context.Fabrics.FindAsync(id);
            if (existing == null) return NotFound();
            fabric.StockQuantity = existing.StockQuantity;
            fabric.Sku = fabric.Sku?.Trim().ToUpperInvariant() ?? string.Empty;

            if (ModelState.IsValid)
            {
                if (!ProductUnits.IsValid(fabric.Unit))
                    ModelState.AddModelError(nameof(fabric.Unit), "Choose a valid inventory unit.");
                if (ProductUnits.IsDiscrete(fabric.Unit) &&
                    (existing.StockQuantity != decimal.Truncate(existing.StockQuantity) ||
                     fabric.ReorderLevel != decimal.Truncate(fabric.ReorderLevel)))
                    ModelState.AddModelError(nameof(fabric.Unit), "Convert the existing stock to a whole quantity before changing to roll or piece.");
                if (await _context.Fabrics.AnyAsync(other => other.FabricId != id && other.Sku == fabric.Sku))
                    ModelState.AddModelError(nameof(fabric.Sku), "This SKU is already in use.");

                if (!ModelState.IsValid)
                {
                    fabric.StockQuantity = existing.StockQuantity;
                    ViewBag.Units = GetUnits();
                    PopulateDropdowns(fabric);
                    return View(fabric);
                }

                existing.Name = fabric.Name;
                existing.Sku = fabric.Sku;
                existing.Color = fabric.Color;
                existing.Pattern = fabric.Pattern;
                existing.CategoryId = fabric.CategoryId;
                existing.SupplierId = fabric.SupplierId;
                existing.Unit = fabric.Unit;
                existing.PricePerUnit = fabric.PricePerUnit;
                existing.ReorderLevel = fabric.ReorderLevel;

                var file = Request.Form.Files.FirstOrDefault();
                string? previousImage = null;
                string? newImagePath = null;

                if (file != null)
                {
                    var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    const long maxBytes = 3 * 1024 * 1024; // 3 MB
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowedExt.Contains(ext))
                    {
                        ModelState.AddModelError("ImageUrl", "Only JPG, PNG and WEBP images are allowed.");
                        fabric.StockQuantity = existing.StockQuantity;
                        ViewBag.Units = GetUnits();
                        PopulateDropdowns(fabric);
                        return View(fabric);
                    }
                    if (file.Length > maxBytes)
                    {
                        ModelState.AddModelError("ImageUrl", "Image must be 3 MB or smaller.");
                        fabric.StockQuantity = existing.StockQuantity;
                        ViewBag.Units = GetUnits();
                        PopulateDropdowns(fabric);
                        return View(fabric);
                    }

                    var uploads = Path.Combine(_env.WebRootPath, "uploads", "fabrics");
                    Directory.CreateDirectory(uploads);
                    var filename = Guid.NewGuid().ToString() + ext;
                    var filepath = Path.Combine(uploads, filename);
                    using var stream = System.IO.File.Create(filepath);
                    await file.CopyToAsync(stream);

                    // set up previous and new paths; do not delete previous yet
                    previousImage = existing.ImageUrl;
                    newImagePath = $"/uploads/fabrics/{filename}";
                    existing.ImageUrl = newImagePath;
                }

                try
                {
                    await _context.SaveChangesAsync();

                    // After successful save, delete previous file if it lived under uploads/fabrics
                    if (!string.IsNullOrEmpty(previousImage))
                    {
                        try
                        {
                            var webRoot = _env.WebRootPath;
                            var uploadsDir = Path.GetFullPath(Path.Combine(webRoot, "uploads", "fabrics")) + Path.DirectorySeparatorChar;
                            var candidate = Path.GetFullPath(Path.Combine(webRoot, previousImage.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                            if (candidate.StartsWith(uploadsDir, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(candidate))
                            {
                                System.IO.File.Delete(candidate);
                            }
                        }
                        catch { /* ignore deletion errors */ }
                    }

                    TempData["Success"] = $"'{fabric.Name}' updated.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Fabrics.Any(f => f.FabricId == id)) return NotFound();
                    throw;
                }
                catch
                {
                    // If save failed and we created a new file, remove the orphan new file
                    if (!string.IsNullOrEmpty(newImagePath))
                    {
                        try
                        {
                            var webRoot = _env.WebRootPath;
                            var uploadsDir = Path.GetFullPath(Path.Combine(webRoot, "uploads", "fabrics")) + Path.DirectorySeparatorChar;
                            var candidate = Path.GetFullPath(Path.Combine(webRoot, newImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                            if (candidate.StartsWith(uploadsDir, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(candidate))
                            {
                                System.IO.File.Delete(candidate);
                            }
                        }
                        catch { }
                    }
                    ModelState.AddModelError(string.Empty, "Updating the fabric failed. Please try again.");
                    PopulateDropdowns(fabric);
                    return View(fabric);
                }
            }
            ViewBag.Units = GetUnits();
            PopulateDropdowns(fabric);
            return View(fabric);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Archive(int id)
        {
            var fabric = await _context.Fabrics.FindAsync(id);
            if (fabric == null) return NotFound();

            fabric.IsActive = false;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"'{fabric.Name}' ({fabric.Sku}) archived. Its sales history remains available.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Restore(int id)
        {
            var fabric = await _context.Fabrics.FindAsync(id);
            if (fabric == null) return NotFound();

            fabric.IsActive = true;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"'{fabric.Name}' ({fabric.Sku}) restored to active inventory.";
            return RedirectToAction(nameof(Index), new { includeArchived = true });
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Restock(int id, decimal quantity, string? reason)
        {
            var fabric = await _context.Fabrics.FindAsync(id);
            if (fabric == null) return NotFound();

            if (!fabric.IsActive)
            {
                TempData["Error"] = "Archived products cannot be restocked. Restore the product first.";
                return RedirectToAction(nameof(Index), new { includeArchived = true });
            }

            if (!IsValidQuantity(fabric.Unit, quantity) || quantity <= 0)
            {
                TempData["Error"] = ProductUnits.IsDiscrete(fabric.Unit)
                    ? "Enter a positive whole number of rolls or pieces to restock."
                    : "Enter a positive stock quantity to restock.";
                return RedirectToAction(nameof(Index));
            }

            if (fabric.StockQuantity + quantity > 99999999.99m)
            {
                TempData["Error"] = "The restocked quantity exceeds the supported stock limit.";
                return RedirectToAction(nameof(Index));
            }

            var notes = string.IsNullOrWhiteSpace(reason) ? "Stock received" : reason.Trim();
            if (notes.Length > 250)
            {
                TempData["Error"] = "The restock note must be 250 characters or fewer.";
                return RedirectToAction(nameof(Index));
            }

            fabric.StockQuantity += quantity;
            _context.InventoryMovements.Add(new InventoryMovement
            {
                FabricId = fabric.FabricId,
                MovementType = "Restock",
                QuantityChange = quantity,
                Notes = notes,
                UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Added {quantity} {fabric.Unit} to '{fabric.Name}'. New stock: {fabric.StockQuantity} {fabric.Unit}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> AdjustStock(int id, decimal quantityChange, string? reason)
        {
            var fabric = await _context.Fabrics.FindAsync(id);
            if (fabric == null) return NotFound();

            if (!fabric.IsActive)
                return BadRequest("Archived products cannot have stock adjustments.");

            if (!IsValidQuantity(fabric.Unit, quantityChange) || quantityChange == 0)
            {
                TempData["Error"] = ProductUnits.IsDiscrete(fabric.Unit)
                    ? "Enter a non-zero whole-number adjustment for rolls or pieces."
                    : "Enter a non-zero stock adjustment.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var adjustedStock = fabric.StockQuantity + quantityChange;
            if (adjustedStock < 0 || adjustedStock > 99999999.99m)
            {
                TempData["Error"] = "The adjustment would result in an invalid stock quantity.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Enter a reason for the stock adjustment.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var notes = reason.Trim();
            if (notes?.Length > 250)
            {
                TempData["Error"] = "The adjustment reason must be 250 characters or fewer.";
                return RedirectToAction(nameof(Details), new { id });
            }

            fabric.StockQuantity = adjustedStock;
            _context.InventoryMovements.Add(new InventoryMovement
            {
                FabricId = fabric.FabricId,
                MovementType = "Adjustment",
                QuantityChange = quantityChange,
                Notes = notes,
                UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Stock for '{fabric.Name}' adjusted by {quantityChange:0.##} {fabric.Unit}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> LowStock()
        {
            var lowStock = await _context.Fabrics.Include(f => f.Category)
                .Include(f => f.Supplier)
                .Where(f => f.IsActive && f.StockQuantity <= f.ReorderLevel)
                .OrderBy(f => f.StockQuantity <= 0 ? 0 : 1)
                .ThenBy(f => f.ReorderLevel > 0 ? f.StockQuantity / f.ReorderLevel : 2m)
                .ThenBy(f => f.Name)
                .ToListAsync();
            return View(lowStock);
        }

        private static bool IsValidQuantity(string unit, decimal quantity) =>
            decimal.Round(quantity, 2) == quantity &&
            (!ProductUnits.IsDiscrete(unit) || quantity == decimal.Truncate(quantity));

        private static SelectList GetUnits() => new(
            new[]
            {
                new SelectListItem("Yard", ProductUnits.Yard),
                new SelectListItem("Meter", ProductUnits.Meter),
                new SelectListItem("Roll", ProductUnits.Roll),
                new SelectListItem("Piece", ProductUnits.Piece)
            },
            nameof(SelectListItem.Value),
            nameof(SelectListItem.Text));

        private void PopulateDropdowns(Fabric? fabric = null)
        {
            ViewBag.CategoryId = new SelectList(_context.Categories, "CategoryId", "Name", fabric?.CategoryId);
            ViewBag.SupplierId = new SelectList(_context.Suppliers, "SupplierId", "Name", fabric?.SupplierId);
        }
    }
}