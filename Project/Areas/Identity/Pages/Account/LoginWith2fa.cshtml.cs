using System.ComponentModel.DataAnnotations;
using FabriTrack.Models;
using FabriTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FabriTrack.Areas.Identity.Pages.Account
{
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

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string ReturnUrl { get; set; } = string.Empty;

        public class InputModel
        {
            [Required]
            [StringLength(10, MinimumLength = 6)]
            [DataType(DataType.Text)]
            [Display(Name = "Authenticator code")]
            public string TwoFactorCode { get; set; } = string.Empty;

            public bool RememberMe { get; set; }

            [Display(Name = "Remember this machine")]
            public bool RememberMachine { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(bool rememberMe, string? returnUrl = null)
        {
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

            var code = Input.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
                code,
                Input.RememberMe,
                Input.RememberMachine);

            if (result.Succeeded)
            {
                await _loginActivityRecorder.RecordAsync(user, "Successful sign-in", user.Email);
                return user.RequirePasswordChange
                    ? RedirectToPage("./ChangePassword")
                    : LocalRedirect(ReturnUrl);
            }

            if (result.IsLockedOut)
            {
                await _loginActivityRecorder.RecordAsync(user, "Account locked out", user.Email);
                return RedirectToPage("./Lockout");
            }

            await _loginActivityRecorder.RecordAsync(user, "Failed sign-in", user.Email);
            ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
            return Page();
        }
    }
}
