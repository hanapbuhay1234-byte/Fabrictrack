# Change password

**Live backend file:** [`ChangePassword.cshtml.cs`](../../../Project/Areas/Identity/Pages/Account/ChangePassword.cshtml.cs)  
**Related view:** [`ChangePassword.cshtml`](../../../Project/Areas/Identity/Pages/Account/ChangePassword.cshtml)

This page lets an authenticated user change their password. It also completes the first-login flow for staff who signed in with a temporary password.

## Complete annotated backend file

```csharp
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
    // Password changes require an authenticated account.
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

        // Form values are bound here and validated using the rules below.
        [BindProperty]
        public InputModel Input { get; set; } = new();

        // The view uses this flag to explain that a temporary password must be replaced.
        public bool IsTemporaryPassword { get; private set; }

        public class InputModel
        {
            [Required, DataType(DataType.Password)]
            [Display(Name = "Current password")]
            public string CurrentPassword { get; set; } = string.Empty;

            // This UI-level minimum is supplemented by Identity's configured password
            // policy (length, character classes, and any configured validators).
            [Required, StringLength(100, MinimumLength = 10)]
            [DataType(DataType.Password)]
            [Display(Name = "New password")]
            public string NewPassword { get; set; } = string.Empty;

            // Prevents a typing mismatch before the server changes the credential.
            [Required, DataType(DataType.Password)]
            [Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation do not match.")]
            [Display(Name = "Confirm new password")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        // GET loads only the first-login indicator; it does not expose credential data.
        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            IsTemporaryPassword = user?.RequirePasswordChange == true;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Resolve the account from the authenticated principal rather than accepting
            // a user ID from the form.
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            IsTemporaryPassword = user.RequirePasswordChange;
            if (!ModelState.IsValid)
                return Page();

            // The password update and the first-login flag change form one unit:
            // if either update fails, the database transaction is rolled back.
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Identity validates the current password, applies password policy,
                // hashes the new password, and updates the security stamp.
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

                // This flag is used by the claims factory and middleware to force
                // temporary-password accounts to this page until the change succeeds.
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

                // Password changes update Identity security data. Refresh the cookie
                // so the current browser session receives the latest claims/stamp.
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Your password has been updated.";
                return Redirect("~/");
            }
            catch
            {
                // Unexpected failures are not hidden; rollback then let normal
                // exception handling/reporting process the exception.
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
```

## Core logic

- The page requires an authenticated user; it never accepts a target user ID from the form.
- Data annotations check required fields, new-password length, and confirmation equality.
- `UserManager.ChangePasswordAsync` verifies the current password and applies Identity's configured validators while changing the password hash.
- The password update and clearing of `RequirePasswordChange` are committed in one database transaction.
- `RefreshSignInAsync` refreshes the current authentication session after the password/security-stamp change.
- `RequirePasswordChange` is cleared only after Identity reports the password update succeeded.
