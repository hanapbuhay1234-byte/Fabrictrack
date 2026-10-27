using System.ComponentModel.DataAnnotations;
using System.Text;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace FabriTrack.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ResetPasswordModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [BindProperty]
        public string? Code { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(100, MinimumLength = 10)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            [Compare(nameof(Password), ErrorMessage = "The password and confirmation do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public IActionResult OnGet(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return RedirectToPage("./ForgotPassword");

            Code = code;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Code))
                ModelState.AddModelError(string.Empty, "The password reset link is invalid or has expired.");

            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
                return RedirectToPage("./ResetPasswordConfirmation");

            string decodedCode;
            try
            {
                decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code!));
            }
            catch (FormatException)
            {
                ModelState.AddModelError(string.Empty, "The password reset link is invalid or has expired.");
                return Page();
            }

            var result = await _userManager.ResetPasswordAsync(user, decodedCode, Input.Password);
            if (result.Succeeded)
                return RedirectToPage("./ResetPasswordConfirmation");

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }
    }
}
