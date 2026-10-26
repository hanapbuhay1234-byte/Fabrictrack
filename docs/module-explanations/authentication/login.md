# Login

**Live backend file:** [`Login.cshtml.cs`](../../../Project/Areas/Identity/Pages/Account/Login.cshtml.cs)  
**Related view:** [`Login.cshtml`](../../../Project/Areas/Identity/Pages/Account/Login.cshtml)

The Razor Page model receives the sign-in form, validates its shape, checks the account's active status, and asks ASP.NET Identity to verify the password. Identity owns the credential verification and failed-attempt lockout behavior.

## Complete annotated backend file

```csharp
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using FabriTrack.Models;
using FabriTrack.Services;

namespace FabriTrack.Areas.Identity.Pages.Account
{
    // Anyone must be able to reach the sign-in page, including signed-out users.
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        // SignInManager is Identity's high-level API for checking credentials
        // and issuing the authentication cookie.
        private readonly SignInManager<ApplicationUser> _signInManager;

        // Login Activity is recorded separately from business Audit Logs.
        private readonly LoginActivityRecorder _loginActivityRecorder;
        private readonly ILogger<LoginModel> _logger;

        // ASP.NET Core dependency injection supplies these services per request.
        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            LoginActivityRecorder loginActivityRecorder,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _loginActivityRecorder = loginActivityRecorder;
            _logger = logger;
        }

        // MVC binds posted form values to this property.
        [BindProperty]
        public InputModel Input { get; set; }

        // Optional external authentication providers, if any are configured.
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        // ReturnUrl remembers which local page the user originally tried to open.
        public string ReturnUrl { get; set; }

        // TempData carries a message across a redirect for one request.
        [TempData]
        public string ErrorMessage { get; set; }

        // Validation rules for values submitted by the sign-in form.
        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        // GET prepares the page. It does not authenticate the user.
        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            // Use the application root when no return destination was provided.
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ReturnUrl = returnUrl;
        }

        // POST processes the submitted email, password, and remember-me choice.
        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            // Re-populate page-only values because a failed POST renders the page again.
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // FindByEmailAsync loads the application user so the app can check
                // FabriTrack-specific activation and password-change fields.
                var user = await _signInManager.UserManager.FindByEmailAsync(Input.Email);

                // Inactive accounts are blocked before checking their password.
                // The response is deliberately generic so the sign-in UI does not
                // explain whether an email belongs to a disabled account.
                if (user != null && !user.IsActive)
                {
                    await _loginActivityRecorder.RecordAsync(
                        user,
                        "Disabled account sign-in blocked",
                        Input.Email);
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }

                // Identity checks the password and, because lockoutOnFailure is true,
                // increments failed attempts and applies lockout according to Identity
                // options. This application does not implement password hashing here.
                var result = await _signInManager.PasswordSignInAsync(
                    Input.Email,
                    Input.Password,
                    Input.RememberMe,
                    lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    // A completed password-only sign-in is recorded here.
                    // With two-factor enabled, Identity does not return Succeeded yet;
                    // the event is recorded by LoginWith2faModel after code verification.
                    await _loginActivityRecorder.RecordAsync(user, "Successful sign-in", Input.Email);
                    _logger.LogInformation("User logged in.");

                    // New or reset staff passwords must be changed before normal use.
                    if (user?.RequirePasswordChange == true)
                        return RedirectToPage("./ChangePassword");

                    // LocalRedirect rejects external destinations in ReturnUrl.
                    return LocalRedirect(returnUrl);
                }

                if (result.RequiresTwoFactor)
                {
                    // Password verification passed, but authentication is incomplete.
                    // Continue to the authenticator-code step; do not record success yet.
                    return RedirectToPage("./LoginWith2fa", new
                    {
                        ReturnUrl = returnUrl,
                        RememberMe = Input.RememberMe
                    });
                }

                if (result.IsLockedOut)
                {
                    // Identity reports when failed attempts have put the account into
                    // lockout. The lockout page gives the user a clear next step.
                    await _loginActivityRecorder.RecordAsync(user, "Account locked out", Input.Email);
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    // Keep the same user-facing message for invalid credentials.
                    await _loginActivityRecorder.RecordAsync(user, "Failed sign-in", Input.Email);
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            // Invalid email/password form fields return to the page for correction.
            return Page();
        }
    }
}
```

## Core logic

1. **Validate input:** Data annotations and `ModelState` reject missing or malformed fields before authentication.
2. **Check account activation:** `IsActive` is FabriTrack's account-level gate. A deactivated user cannot proceed to Identity password verification.
3. **Delegate credentials to Identity:** `PasswordSignInAsync` compares the password hash and handles failure counts. `lockoutOnFailure: true` enables lockout processing for failed password attempts.
4. **Branch on `SignInResult`:**
   - `Succeeded`: authentication is complete for password-only accounts.
   - `RequiresTwoFactor`: password was accepted, but sign-in is not complete.
   - `IsLockedOut`: route to the lockout screen.
   - Otherwise: show a generic invalid-login message.
5. **Enforce first-login password change:** after a successful sign-in, `RequirePasswordChange` routes the user to Change Password.

The optional `LoginActivityRecorder` calls do not pass or store `Input.Password`.
