using System.ComponentModel.DataAnnotations;
using System.Text;
using FabriTrack.Models;
using FabriTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace FabriTrack.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPasswordResetEmailSender _emailSender;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager,
            IPasswordResetEmailSender emailSender,
            IConfiguration configuration,
            ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            if (!_emailSender.IsConfigured)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Password recovery email is not configured. Please contact your administrator.");
                return Page();
            }

            var publicBaseUrl = GetPublicBaseUrl();
            if (publicBaseUrl == null)
            {
                _logger.LogError("Password reset cannot be sent because App:PublicBaseUrl is not a valid HTTPS URL.");
                ModelState.AddModelError(
                    string.Empty,
                    "Password recovery is not configured correctly. Please contact your administrator.");
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null || !user.EmailConfirmed)
                return RedirectToPage("./ForgotPasswordConfirmation");

            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var resetUrl = Url.Page(
                    "./ResetPassword",
                    pageHandler: null,
                    values: new { code },
                    protocol: publicBaseUrl.Scheme,
                    host: publicBaseUrl.Authority);

                if (string.IsNullOrWhiteSpace(resetUrl))
                    throw new InvalidOperationException("Could not generate the password reset URL.");

                await _emailSender.SendResetLinkAsync(user.Email!, resetUrl);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or System.Net.Mail.SmtpException or FormatException)
            {
                _logger.LogError(exception, "Unable to send a password reset email for user {UserId}.", user.Id);
            }

            return RedirectToPage("./ForgotPasswordConfirmation");
        }

        private Uri? GetPublicBaseUrl()
        {
            var configuredUrl = _configuration["App:PublicBaseUrl"];
            if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var publicBaseUrl) ||
                !string.Equals(publicBaseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(publicBaseUrl.UserInfo) ||
                !string.IsNullOrEmpty(publicBaseUrl.Query) ||
                !string.IsNullOrEmpty(publicBaseUrl.Fragment))
            {
                return null;
            }

            return publicBaseUrl;
        }
    }
}
