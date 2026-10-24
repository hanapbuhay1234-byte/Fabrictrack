using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FabriTrack.Controllers
{
    [Authorize]
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CustomersController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            var model = await BuildIndexViewModel(search, page);
            return View(model);
        }

        public async Task<IActionResult> Details(int id, int page = 1)
        {
            var customer = await _context.Customers
                .AsNoTracking()
                .Where(item => item.CustomerId == id)
                .Select(item => new CustomerDetailsViewModel
                {
                    CustomerId = item.CustomerId,
                    FullName = item.FullName,
                    ContactNumber = item.ContactNumber,
                    Email = item.Email,
                    Address = item.Address,
                    CompletedPurchaseCount = item.Sales.Count(sale => !sale.IsVoided),
                    LifetimeSpend = item.Sales.Where(sale => !sale.IsVoided).Sum(sale => (decimal?)sale.TotalAmount) ?? 0m,
                    TotalPurchases = item.Sales.Count(sale => !sale.IsVoided)
                })
                .FirstOrDefaultAsync();
            if (customer == null) return NotFound();
            customer.Page = Math.Clamp(page, 1, customer.TotalPages);
            customer.Purchases = await _context.Sales
                .AsNoTracking()
                .Where(sale => sale.CustomerId == id && !sale.IsVoided)
                .OrderByDescending(sale => sale.SaleDate)
                .ThenByDescending(sale => sale.SaleId)
                .Skip((customer.Page - 1) * customer.PageSize)
                .Take(customer.PageSize)
                .Select(sale => new CustomerPurchaseViewModel
                {
                    SaleId = sale.SaleId,
                    SaleDate = sale.SaleDate,
                    TotalAmount = sale.TotalAmount
                })
                .ToListAsync();
            return View(customer);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind(Prefix = "CreateCustomer")] Customer customer,
            string? search,
            int page = 1)
        {
            if (ModelState.IsValid)
            {
                _context.Add(customer);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"'{customer.FullName}' added.";
                return RedirectToAction(nameof(Index), new { search, page });
            }

            var viewModel = await BuildIndexViewModel(search, page);
            viewModel.CreateCustomer = customer;
            viewModel.OpenCreateModal = true;
            return View(nameof(Index), viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Edit(
            int id,
            [Bind(Prefix = "EditCustomer")] Customer customer,
            string? search,
            int page = 1)
        {
            if (id != customer.CustomerId) return NotFound();
            if (ModelState.IsValid)
            {
                var existingCustomer = await _context.Customers.FindAsync(id);
                if (existingCustomer == null) return NotFound();
                existingCustomer.FullName = customer.FullName;
                existingCustomer.ContactNumber = customer.ContactNumber;
                existingCustomer.Email = customer.Email;
                existingCustomer.Address = customer.Address;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"'{customer.FullName}' updated.";
                return RedirectToAction(nameof(Index), new { search, page });
            }

            var viewModel = await BuildIndexViewModel(search, page);
            viewModel.EditCustomer = customer;
            viewModel.OpenEditModal = true;
            return View(nameof(Index), viewModel);
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                try
                {
                    _context.Customers.Remove(customer);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"'{customer.FullName}' deleted.";
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = $"Can't delete '{customer.FullName}' — they have sales on record.";
                }
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<CustomersIndexViewModel> BuildIndexViewModel(string? search, int page)
        {
            var model = new CustomersIndexViewModel
            {
                Search = search?.Trim(),
                Page = Math.Max(1, page)
            };
            var query = _context.Customers.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(model.Search))
            {
                var searchTerm = model.Search;
                query = query.Where(customer =>
                    customer.FullName.Contains(searchTerm) ||
                    (customer.ContactNumber != null && customer.ContactNumber.Contains(searchTerm)) ||
                    (customer.Email != null && customer.Email.Contains(searchTerm)));
            }

            model.TotalCustomers = await query.CountAsync();
            var matchingCustomerIds = query.Select(customer => customer.CustomerId);
            var completedSales = _context.Sales
                .Where(sale => !sale.IsVoided && sale.CustomerId.HasValue && matchingCustomerIds.Contains(sale.CustomerId.Value));
            model.CompletedPurchases = await completedSales.CountAsync();
            model.CompletedSpend = await completedSales.SumAsync(sale => (decimal?)sale.TotalAmount) ?? 0m;
            model.Page = Math.Min(model.Page, model.TotalPages);
            model.Customers = await query
                .OrderBy(customer => customer.FullName)
                .ThenBy(customer => customer.CustomerId)
                .Skip((model.Page - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(customer => new CustomerListItemViewModel
                {
                    CustomerId = customer.CustomerId,
                    FullName = customer.FullName,
                    ContactNumber = customer.ContactNumber,
                    Email = customer.Email,
                    Address = customer.Address,
                    CompletedPurchaseCount = customer.Sales.Count(sale => !sale.IsVoided),
                    LifetimeSpend = customer.Sales.Where(sale => !sale.IsVoided).Sum(sale => (decimal?)sale.TotalAmount) ?? 0m
                })
                .ToListAsync();
            return model;
        }
    }
}
