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

        public async Task<IActionResult> Index() => View(await BuildIndexViewModel());

        private async Task<CategoriesIndexViewModel> BuildIndexViewModel()
        {
            // Load the product counts along with the category list.
            var categories = await _context.Categories
                .OrderBy(category => category.Name)
                .Select(category => new CategoryListItemViewModel
                {
                    CategoryId = category.CategoryId,
                    Name = category.Name,
                    Description = category.Description,
                    ActiveProductCount = category.Fabrics.Count(fabric => fabric.IsActive),
                    ArchivedProductCount = category.Fabrics.Count(fabric => !fabric.IsActive)
                })
                .ToListAsync();

            return new CategoriesIndexViewModel { Categories = categories };
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "CreateCategory")] Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Add(category);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"'{category.Name}' added.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = await BuildIndexViewModel();
            viewModel.CreateCategory = category;
            viewModel.OpenCreateModal = true;
            return View(nameof(Index), viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind(Prefix = "EditCategory")] Category category)
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

            var viewModel = await BuildIndexViewModel();
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