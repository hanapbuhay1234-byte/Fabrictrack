# Authenticator sign-in

**Live backend file:** [`LoginWith2fa.cshtml.cs`](../../../Project/Areas/Identity/Pages/Account/LoginWith2fa.cshtml.cs)  
**Related view:** [`LoginWith2fa.cshtml`](../../../Project/Areas/Identity/Pages/Account/LoginWith2fa.cshtml)

This page completes an Identity sign-in that has already passed password verification but requires an authenticator code. The pending two-factor user is held by ASP.NET Identity; the page does not identify the user from a posted user ID.

## Complete annotated backend file

```csharp
using System.ComponentModel.DataAnnotations;
using FabriTrack.Models;
using FabriTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FabriTrack.Areas.Identity.Pages.Account
{
    // The user is not fully authenticated until the second factor succeeds.
    [AllowAnonymous]
    public class LoginWith2faModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly LoginActivityRecorder _loginActivityRecorder;

        public LoginWith2faModel(
            SignInManager<ApplicationUser> signInManager,
            LoginActivityRecorder loginActivityRecorder)
        {
            _signInManager = signInManager;
            _loginActivityRecorder = loginActivityRecorder;
        }

        // Binds the authenticator code and remember choices from the form.
        [BindProperty]
        public InputModel Input { get; set; } = new();

        // Destination preserved from the original login request.
        public string ReturnUrl { get; set; } = string.Empty;

        public class InputModel
        {
            [Required]
            [StringLength(10, MinimumLength = 6)]
            [DataType(DataType.Text)]
            [Display(Name = "Authenticator code")]
            public string TwoFactorCode { get; set; } = string.Empty;

            // Preserves the original "Remember me" selection.
            public bool RememberMe { get; set; }

            // Asks Identity to remember this device if its implementation supports it.
            [Display(Name = "Remember this machine")]
            public bool RememberMachine { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(bool rememberMe, string? returnUrl = null)
        {
            // Identity stores the pending two-factor user after the password step.
            // If no such pending sign-in exists, this request is not a valid 2FA flow.
            if (await _signInManager.GetTwoFactorAuthenticationUserAsync() == null)
                throw new InvalidOperationException("Unable to load the two-factor authentication user.");

            ReturnUrl = returnUrl ?? Url.Content("~/");
            Input.RememberMe = rememberMe;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
                throw new InvalidOperationException("Unable to load the two-factor authentication user.");

            ReturnUrl = returnUrl ?? Url.Content("~/");
            if (!ModelState.IsValid)
                return Page();

            // Authenticator codes are commonly displayed with spaces or dashes.
            // Remove those formatting characters before asking Identity to verify.
            var code = Input.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
                code,
                Input.RememberMe,
                Input.RememberMachine);

            if (result.Succeeded)
            {
                // A successful event is recorded only after the second factor passes.
                await _loginActivityRecorder.RecordAsync(user, "Successful sign-in", user.Email);

                // The forced password-change requirement also applies to 2FA accounts.
                return user.RequirePasswordChange
                    ? RedirectToPage("./ChangePassword")
                    : LocalRedirect(ReturnUrl);
            }

            if (result.IsLockedOut)
            {
                // Identity may lock an account after repeated failed authenticator codes.
                await _loginActivityRecorder.RecordAsync(user, "Account locked out", user.Email);
                return RedirectToPage("./Lockout");
            }

            await _loginActivityRecorder.RecordAsync(user, "Failed sign-in", user.Email);
            ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
            return Page();
        }
    }
}
```

## Core logic

- The pending user comes from `GetTwoFactorAuthenticationUserAsync`; a browser cannot choose another user's identity by submitting an ID.
- `TwoFactorAuthenticatorSignInAsync` validates the code and creates the authenticated session only on success.
- The successful login activity event is recorded after 2FA succeeds, not immediately after the password step.
- Staff accounts requiring a new password are sent to Change Password after the full sign-in is complete.
