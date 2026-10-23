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

        public async Task<IActionResult> Index(string? search, int? categoryId)
        {
            var fabrics = _context.Fabrics.Include(f => f.Category).Include(f => f.Supplier).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                fabrics = fabrics.Where(f => f.Name.Contains(search) || (f.Color != null && f.Color.Contains(search)));

            if (categoryId.HasValue)
                fabrics = fabrics.Where(f => f.CategoryId == categoryId);

            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "CategoryId", "Name");
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;

            return View(await fabrics.OrderBy(f => f.Name).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var fabric = await _context.Fabrics.Include(f => f.Category).Include(f => f.Supplier)
                .FirstOrDefaultAsync(f => f.FabricId == id);
            if (fabric == null) return NotFound();
            return View(fabric);
        }

        [Authorize(Roles = Roles.Admin)]
        public IActionResult Create()
        {
            PopulateDropdowns();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Create(Fabric fabric)
        {
            var file = Request.Form.Files.FirstOrDefault();
            // Validate file if present (image is OPTIONAL on create)
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

                _context.Add(fabric);
                try
                {
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"'{fabric.Name}' added to inventory.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException)
                {
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
                    ModelState.AddModelError(string.Empty, "Saving the fabric failed. Please try again.");
                }
            }
            PopulateDropdowns(fabric);
            return View(fabric);
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Edit(int id)
        {
            var fabric = await _context.Fabrics.FindAsync(id);
            if (fabric == null) return NotFound();
            PopulateDropdowns(fabric);
            return View(fabric);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Edit(int id, Fabric fabric)
        {
            if (id != fabric.FabricId) return NotFound();

            if (ModelState.IsValid)
            {
                var existing = await _context.Fabrics.FindAsync(id);
                if (existing == null) return NotFound();

                // update scalar properties
                existing.Name = fabric.Name;
                existing.Color = fabric.Color;
                existing.Pattern = fabric.Pattern;
                existing.CategoryId = fabric.CategoryId;
                existing.SupplierId = fabric.SupplierId;
                existing.Unit = fabric.Unit;
                existing.PricePerUnit = fabric.PricePerUnit;
                existing.StockQuantity = fabric.StockQuantity;

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
                        PopulateDropdowns(fabric);
                        return View(fabric);
                    }
                    if (file.Length > maxBytes)
                    {
                        ModelState.AddModelError("ImageUrl", "Image must be 3 MB or smaller.");
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
            PopulateDropdowns(fabric);
            return View(fabric);
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var fabric = await _context.Fabrics.Include(f => f.Category).Include(f => f.Supplier)
                .FirstOrDefaultAsync(f => f.FabricId == id);
            if (fabric == null) return NotFound();
            return View(fabric);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var fabric = await _context.Fabrics.FindAsync(id);
            if (fabric != null)
            {
                try
                {
                    // Remove fabric record first; only delete image after successful DB save
                    _context.Fabrics.Remove(fabric);
                    await _context.SaveChangesAsync();
                    // After successful save, delete associated image file if it exists under uploads/fabrics
                    if (!string.IsNullOrEmpty(fabric.ImageUrl))
                    {
                        try
                        {
                            var webRoot = _env.WebRootPath;
                            var uploadsDir = Path.GetFullPath(Path.Combine(webRoot, "uploads", "fabrics")) + Path.DirectorySeparatorChar;
                            var candidate = Path.GetFullPath(Path.Combine(webRoot, fabric.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                            if (candidate.StartsWith(uploadsDir, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(candidate))
                            {
                                System.IO.File.Delete(candidate);
                            }
                        }
                        catch { /* ignore file deletion errors */ }
                    }
                    TempData["Success"] = $"'{fabric.Name}' removed from inventory.";
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = $"Can't delete '{fabric.Name}' — it appears in past sales records.";
                }
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Restock(int id, decimal quantity)
        {
            var fabric = await _context.Fabrics.FindAsync(id);
            if (fabric == null) return NotFound();

            if (quantity <= 0)
            {
                TempData["Error"] = "Enter a quantity greater than 0 to restock.";
                return RedirectToAction(nameof(Index));
            }

            fabric.StockQuantity += quantity;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Added {quantity} {fabric.Unit} to '{fabric.Name}'. New stock: {fabric.StockQuantity} {fabric.Unit}.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> LowStock()
        {
            var lowStock = await _context.Fabrics.Include(f => f.Category)
                .Where(f => f.StockQuantity <= 0)
                .OrderBy(f => f.StockQuantity)
                .ToListAsync();
            return View(lowStock);
        }

        private void PopulateDropdowns(Fabric? fabric = null)
        {
            ViewBag.CategoryId = new SelectList(_context.Categories, "CategoryId", "Name", fabric?.CategoryId);
            ViewBag.SupplierId = new SelectList(_context.Suppliers, "SupplierId", "Name", fabric?.SupplierId);
        }
    }
}