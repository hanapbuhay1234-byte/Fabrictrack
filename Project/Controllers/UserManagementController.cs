using System.Security.Cryptography;
using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FabriTrack.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class UserManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserManagementController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            var model = await BuildViewModelAsync(search, page);
            model.TemporaryPassword = TempData["TemporaryStaffPassword"] as string;
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind(Prefix = "CreateStaff")] CreateStaffViewModel input,
            string? search,
            int page = 1)
        {
            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildViewModelAsync(search, page);
                invalidModel.CreateStaff = input;
                invalidModel.OpenCreateModal = true;
                return View(nameof(Index), invalidModel);
            }

            var staffRole = await _roleManager.FindByNameAsync(Roles.Staff);
            if (staffRole == null)
            {
                ModelState.AddModelError(string.Empty, "The Staff role is not configured. Contact the system administrator.");
                var errorModel = await BuildViewModelAsync(search, page);
                errorModel.CreateStaff = input;
                errorModel.OpenCreateModal = true;
                return View(nameof(Index), errorModel);
            }

            var temporaryPassword = GenerateTemporaryPassword();
            var user = new ApplicationUser
            {
                UserName = input.Email.Trim(),
                Email = input.Email.Trim(),
                FullName = input.FullName.Trim(),
                EmailConfirmed = true,
                IsActive = true,
                RequirePasswordChange = true
            };
            IdentityResult? identityFailure = null;

            await using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var createResult = await _userManager.CreateAsync(user, temporaryPassword);
                    if (!createResult.Succeeded)
                    {
                        identityFailure = createResult;
                        await transaction.RollbackAsync();
                    }
                    else
                    {
                        var roleResult = await _userManager.AddToRoleAsync(user, Roles.Staff);
                        if (!roleResult.Succeeded)
                        {
                            identityFailure = roleResult;
                            await transaction.RollbackAsync();
                        }
                        else
                        {
                            await transaction.CommitAsync();
                        }
                    }
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            if (identityFailure != null)
            {
                AddIdentityErrors(identityFailure);
                var errorModel = await BuildViewModelAsync(search, page);
                errorModel.CreateStaff = input;
                errorModel.OpenCreateModal = true;
                return View(nameof(Index), errorModel);
            }

            TempData["TemporaryStaffPassword"] = temporaryPassword;
            TempData["Success"] = $"Staff account for '{user.FullName}' created. Share the one-time temporary password securely.";
            return RedirectToAction(nameof(Index), new { search, page });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            [Bind(Prefix = "EditStaff")] EditStaffViewModel input,
            string? search,
            int page = 1)
        {
            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildViewModelAsync(search, page);
                invalidModel.EditStaff = input;
                invalidModel.OpenEditModal = true;
                return View(nameof(Index), invalidModel);
            }

            var user = await FindStaffAsync(input.Id);
            if (user == null) return NotFound();

            user.FullName = input.FullName.Trim();
            user.Email = input.Email.Trim();
            user.UserName = input.Email.Trim();
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                var errorModel = await BuildViewModelAsync(search, page);
                errorModel.EditStaff = input;
                errorModel.OpenEditModal = true;
                return View(nameof(Index), errorModel);
            }

            TempData["Success"] = $"Staff account for '{user.FullName}' updated.";
            return RedirectToAction(nameof(Index), new { search, page });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(
            string id,
            bool isActive,
            string? search,
            int page = 1)
        {
            var user = await FindStaffAsync(id);
            if (user == null) return NotFound();
            if (user.IsActive == isActive)
            {
                TempData["Error"] = $"'{user.FullName}' is already {(isActive ? "active" : "deactivated")}.";
                return RedirectToAction(nameof(Index), new { search, page });
            }

            user.IsActive = isActive;
            user.SecurityStamp = Guid.NewGuid().ToString();
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                SetIdentityErrors(result);
                return RedirectToAction(nameof(Index), new { search, page });
            }

            TempData["Success"] = isActive
                ? $"'{user.FullName}' can sign in again."
                : $"'{user.FullName}' was deactivated and existing sessions invalidated.";
            return RedirectToAction(nameof(Index), new { search, page });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id, string? search, int page = 1)
        {
            var user = await FindStaffAsync(id);
            if (user == null) return NotFound();

            var temporaryPassword = GenerateTemporaryPassword();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, token, temporaryPassword);
                if (!resetResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    SetIdentityErrors(resetResult);
                    return RedirectToAction(nameof(Index), new { search, page });
                }

                user.RequirePasswordChange = true;
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    SetIdentityErrors(updateResult);
                    return RedirectToAction(nameof(Index), new { search, page });
                }

                await transaction.CommitAsync();
                TempData["TemporaryStaffPassword"] = temporaryPassword;
                TempData["Success"] = $"Password reset for '{user.FullName}'. Share the one-time temporary password securely.";
                return RedirectToAction(nameof(Index), new { search, page });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<UserManagementViewModel> BuildViewModelAsync(string? search, int page)
        {
            var model = new UserManagementViewModel
            {
                Search = search?.Trim(),
                Page = Math.Max(1, page)
            };
            var staffRole = await _roleManager.FindByNameAsync(Roles.Staff);
            if (staffRole == null) return model;

            var staffQuery = _userManager.Users
                .Where(user => _context.UserRoles.Any(userRole =>
                    userRole.UserId == user.Id && userRole.RoleId == staffRole.Id));
            var adminRole = await _roleManager.FindByNameAsync(Roles.Admin);
            if (adminRole != null)
            {
                staffQuery = staffQuery.Where(user => !_context.UserRoles.Any(userRole =>
                    userRole.UserId == user.Id && userRole.RoleId == adminRole.Id));
            }
            if (!string.IsNullOrWhiteSpace(model.Search))
            {
                var searchTerm = model.Search;
                staffQuery = staffQuery.Where(user =>
                    user.FullName.Contains(searchTerm) ||
                    (user.Email != null && user.Email.Contains(searchTerm)));
            }

            model.TotalStaff = await staffQuery.CountAsync();
            model.ActiveStaff = await staffQuery.CountAsync(user => user.IsActive);
            model.InactiveStaff = model.TotalStaff - model.ActiveStaff;
            model.Page = Math.Min(model.Page, model.TotalPages);
            model.Staff = await staffQuery
                .OrderBy(user => user.FullName)
                .ThenBy(user => user.Id)
                .Skip((model.Page - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(user => new StaffUserRowViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email ?? string.Empty,
                    IsActive = user.IsActive,
                    RequirePasswordChange = user.RequirePasswordChange
                })
                .ToListAsync();
            return model;
        }

        private async Task<ApplicationUser?> FindStaffAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            return user != null &&
                await _userManager.IsInRoleAsync(user, Roles.Staff) &&
                !await _userManager.IsInRoleAsync(user, Roles.Admin)
                    ? user
                    : null;
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
        }

        private void SetIdentityErrors(IdentityResult result)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(error => error.Description));
        }

        private static string GenerateTemporaryPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "23456789";
            const string symbols = "!@$%*-_";
            const string all = upper + lower + digits + symbols;
            var characters = new List<char>
            {
                upper[RandomNumberGenerator.GetInt32(upper.Length)],
                lower[RandomNumberGenerator.GetInt32(lower.Length)],
                digits[RandomNumberGenerator.GetInt32(digits.Length)],
                symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
            };
            for (var index = 0; index < 12; index++)
                characters.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);

            for (var index = characters.Count - 1; index > 0; index--)
            {
                var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
                (characters[index], characters[swapIndex]) = (characters[swapIndex], characters[index]);
            }

            return new string(characters.ToArray());
        }
    }
}
