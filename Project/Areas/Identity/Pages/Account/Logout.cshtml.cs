using FabriTrack.Models;
using FabriTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FabriTrack.Areas.Identity.Pages.Account
{
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

        public IActionResult OnGet()
        {
            return LocalRedirect(Url.Content("~/"));
        }

        public async Task<IActionResult> OnPost(string? returnUrl = null)
        {
            var user = await _signInManager.UserManager.GetUserAsync(User);
            if (user != null &&
                !await _loginActivityRecorder.RecordAsync(user, "Signed out", user.Email))
            {
                TempData["Error"] = "You were signed out, but the login activity could not be recorded. The error was logged.";
            }

            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            return string.IsNullOrWhiteSpace(returnUrl)
                ? LocalRedirect(Url.Content("~/"))
                : LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/"));
        }
    }
}
