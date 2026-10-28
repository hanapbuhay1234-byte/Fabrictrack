using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FabriTrack.Controllers
{
    // Lets admins manage categories. A category in use cannot be deleted.
    [Authorize(Roles = Roles.Admin)]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CategoriesController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index(string? search, string? sort)
            => View(await BuildIndexViewModel(search, sort));

        private async Task<CategoriesIndexViewModel> BuildIndexViewModel(string? search = null, string? sort = null)
        {
            var normalizedSort = string.IsNullOrWhiteSpace(sort) ? "date-desc" : sort.Trim().ToLowerInvariant();
            var normalizedSearch = search?.Trim();
            var query = _context.Categories.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
                query = query.Where(category => category.Name.Contains(normalizedSearch) ||
                    (category.Description != null && category.Description.Contains(normalizedSearch)));

            var categories = await query.Select(category => new CategoryListItemViewModel
            {
                CategoryId = category.CategoryId,
                Name = category.Name,
                Description = category.Description,
                CreatedAt = category.CreatedAt,
                ActiveProductCount = category.Fabrics.Count(fabric => fabric.IsActive),
                ArchivedProductCount = category.Fabrics.Count(fabric => !fabric.IsActive)
            }).ToListAsync();

            categories = normalizedSort switch
            {
                "name-asc" => categories.OrderBy(category => category.Name).ToList(),
                "name-desc" => categories.OrderByDescending(category => category.Name).ToList(),
                "date-asc" => categories.OrderBy(category => category.CreatedAt).ToList(),
                "active-desc" => categories.OrderByDescending(category => category.ActiveProductCount).ThenBy(category => category.Name).ToList(),
                "active-asc" => categories.OrderBy(category => category.ActiveProductCount).ThenBy(category => category.Name).ToList(),
                _ => categories.OrderByDescending(category => category.CreatedAt).ThenByDescending(category => category.CategoryId).ToList()
            };

            return new CategoriesIndexViewModel
            {
                Categories = categories,
                Search = normalizedSearch,
                Sort = normalizedSort == "name-asc" || normalizedSort == "name-desc" || normalizedSort == "date-asc" ||
                    normalizedSort == "active-desc" || normalizedSort == "active-asc"
                    ? normalizedSort : "date-desc"
            };
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "CreateCategory")] Category category, string? search, string? sort)
        {
            if (ModelState.IsValid)
            {
                _context.Add(category);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"'{category.Name}' added.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = await BuildIndexViewModel(search, sort);
            viewModel.CreateCategory = category;
            viewModel.OpenCreateModal = true;
            return View(nameof(Index), viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind(Prefix = "EditCategory")] Category category, string? search, string? sort)
        {
            if (category.CategoryId <= 0) return NotFound();
            if (ModelState.IsValid)
            {
                var existingCategory = await _context.Categories.FindAsync(category.CategoryId);
                if (existingCategory == null) return NotFound();

                existingCategory.Name = category.Name;
                existingCategory.Description = category.Description;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"'{category.Name}' updated.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = await BuildIndexViewModel(search, sort);
            viewModel.EditCategory = category;
            viewModel.OpenEditModal = true;
            return View(nameof(Index), viewModel);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                // Products must be moved to another category before this one can be deleted.
                var productCount = await _context.Fabrics.CountAsync(fabric => fabric.CategoryId == id);
                if (productCount > 0)
                {
                    TempData["Error"] = $"Can't delete '{category.Name}' because it is assigned to {productCount} product{(productCount == 1 ? "" : "s")}. Reassign those products first.";
                    return RedirectToAction(nameof(Index));
                }

                try
                {
                    _context.Categories.Remove(category);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"'{category.Name}' deleted.";
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = $"Can't delete '{category.Name}' because it is still referenced by other records.";
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}