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

        public async Task<IActionResult> Index() => View(await _context.Suppliers.ToListAsync());

        public IActionResult Create() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier supplier)
        {
            if (ModelState.IsValid)
            {
                _context.Add(supplier);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"'{supplier.Name}' added.";
                return RedirectToAction(nameof(Index));
            }
            return View(supplier);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();
            return View(supplier);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Supplier supplier)
        {
            if (id != supplier.SupplierId) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(supplier);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"'{supplier.Name}' updated.";
                return RedirectToAction(nameof(Index));
            }
            return View(supplier);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();
            return View(supplier);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                try
                {
                    _context.Suppliers.Remove(supplier);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"'{supplier.Name}' deleted.";
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = $"Can't delete '{supplier.Name}' — it's still assigned to one or more fabrics.";
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
