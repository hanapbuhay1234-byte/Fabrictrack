using System.ComponentModel.DataAnnotations;
using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FabriTrack.Areas.Identity.Pages.Account
{
    [Authorize]
    public class ChangePasswordModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public ChangePasswordModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public bool IsTemporaryPassword { get; private set; }

        public class InputModel
        {
            [Required, DataType(DataType.Password)]
            [Display(Name = "Current password")]
            public string CurrentPassword { get; set; } = string.Empty;

            [Required, StringLength(100, MinimumLength = 10)]
            [DataType(DataType.Password)]
            [Display(Name = "New password")]
            public string NewPassword { get; set; } = string.Empty;

            [Required, DataType(DataType.Password)]
            [Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation do not match.")]
            [Display(Name = "Confirm new password")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            IsTemporaryPassword = user?.RequirePasswordChange == true;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            IsTemporaryPassword = user.RequirePasswordChange;
            if (!ModelState.IsValid)
                return Page();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var passwordResult = await _userManager.ChangePasswordAsync(
                    user,
                    Input.CurrentPassword,
                    Input.NewPassword);
                if (!passwordResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    foreach (var error in passwordResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return Page();
                }

                user.RequirePasswordChange = false;
                var userResult = await _userManager.UpdateAsync(user);
                if (!userResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    foreach (var error in userResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return Page();
                }

                await transaction.CommitAsync();
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Your password has been updated.";
                return Redirect("~/");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
