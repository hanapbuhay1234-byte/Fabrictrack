# Logout

**Live backend file:** [`Logout.cshtml.cs`](../../../Project/Areas/Identity/Pages/Account/Logout.cshtml.cs)  
**Related page:** [`Logout.cshtml`](../../../Project/Areas/Identity/Pages/Account/Logout.cshtml)

Logout is an authenticated Identity Razor Page. The profile menu sends a POST request; the server records an explicit sign-out event and then clears the Identity session.

## Complete annotated backend file

```csharp
using FabriTrack.Models;
using FabriTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FabriTrack.Areas.Identity.Pages.Account
{
    // Only an authenticated session can explicitly sign out.
    [Authorize]
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly LoginActivityRecorder _loginActivityRecorder;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(
            SignInManager<ApplicationUser> signInManager,
            LoginActivityRecorder loginActivityRecorder,
            ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _loginActivityRecorder = loginActivityRecorder;
            _logger = logger;
        }

        // GET does not sign the user out. The actual state-changing operation is POST.
        public IActionResult OnGet()
        {
            return LocalRedirect(Url.Content("~/"));
        }

        // The profile menu submits this POST after the user confirms the logout dialog.
        public async Task<IActionResult> OnPost(string? returnUrl = null)
        {
            // Resolve the user from the authenticated principal, not from posted data.
            var user = await _signInManager.UserManager.GetUserAsync(User);
            if (user != null &&
                !await _loginActivityRecorder.RecordAsync(user, "Signed out", user.Email))
            {
                // Logging failure is made visible, but it does not strand the user
                // in an authenticated state or prevent the requested sign-out.
                TempData["Error"] = "You were signed out, but the login activity could not be recorded. The error was logged.";
            }

            // Clears the application authentication cookie.
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");

            // Only redirect to a local return URL to avoid open redirects.
            return string.IsNullOrWhiteSpace(returnUrl)
                ? LocalRedirect(Url.Content("~/"))
                : LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/"));
        }
    }
}
```

## Core logic

1. The account identity is derived from the authenticated request.
2. `LoginActivityRecorder` attempts to persist the explicit sign-out event.
3. Even if activity persistence fails, the user is still signed out; the failure is logged and a message is queued.
4. The return destination is checked with `Url.IsLocalUrl` before redirecting.

This records an explicit logout only. It does not claim that a user signed out when a browser closes or a session expires.
