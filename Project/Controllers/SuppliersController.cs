using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FabriTrack.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class SuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;
        public SuppliersController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index() => View(await BuildIndexViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "CreateSupplier")] Supplier supplier)
        {
            if (ModelState.IsValid)
            {
                _context.Add(supplier);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"'{supplier.Name}' added.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = await BuildIndexViewModel();
            viewModel.CreateSupplier = supplier;
            viewModel.OpenCreateModal = true;
            return View(nameof(Index), viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind(Prefix = "EditSupplier")] Supplier supplier)
        {
            if (supplier.SupplierId <= 0) return NotFound();
            if (ModelState.IsValid)
            {
                var existingSupplier = await _context.Suppliers.FindAsync(supplier.SupplierId);
                if (existingSupplier == null) return NotFound();

                existingSupplier.Name = supplier.Name;
                existingSupplier.ContactNumber = supplier.ContactNumber;
                existingSupplier.Email = supplier.Email;
                existingSupplier.Address = supplier.Address;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"'{supplier.Name}' updated.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = await BuildIndexViewModel();
            viewModel.EditSupplier = supplier;
            viewModel.OpenEditModal = true;
            return View(nameof(Index), viewModel);
        }

        private async Task<SuppliersIndexViewModel> BuildIndexViewModel()
        {
            var suppliers = await _context.Suppliers
                .OrderBy(supplier => supplier.Name)
                .Select(supplier => new SupplierListItemViewModel
                {
                    SupplierId = supplier.SupplierId,
                    Name = supplier.Name,
                    ContactNumber = supplier.ContactNumber,
                    Email = supplier.Email,
                    Address = supplier.Address,
                    ActiveProductCount = supplier.Fabrics.Count(fabric => fabric.IsActive),
                    ArchivedProductCount = supplier.Fabrics.Count(fabric => !fabric.IsActive)
                })
                .ToListAsync();

            return new SuppliersIndexViewModel { Suppliers = suppliers };
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                var productCount = await _context.Fabrics.CountAsync(fabric => fabric.SupplierId == id);
                if (productCount > 0)
                {
                    TempData["Error"] = $"Can't delete '{supplier.Name}' because it is assigned to {productCount} product{(productCount == 1 ? "" : "s")}. Reassign those products first.";
                    return RedirectToAction(nameof(Index));
                }

                try
                {
                    _context.Suppliers.Remove(supplier);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"'{supplier.Name}' deleted.";
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = $"Can't delete '{supplier.Name}' because it is still referenced by other records.";
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
