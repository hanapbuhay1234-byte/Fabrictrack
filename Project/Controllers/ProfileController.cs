using System.Text.RegularExpressions;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FabriTrack.Controllers
{
    [Authorize(Roles = Roles.Admin + "," + Roles.Staff)]
    public class ProfileController : Controller
    {
        private const long MaxPhotoBytes = 5 * 1024 * 1024;
        private static readonly string[] AllowedPhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment environment,
            ILogger<ProfileController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            return View(CreateViewModel(user));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(ProfileViewModel input)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var email = input.Email?.Trim() ?? string.Empty;
            var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
            if (emailChanged)
            {
                if (string.IsNullOrWhiteSpace(input.CurrentPassword))
                {
                    ModelState.AddModelError(nameof(input.CurrentPassword), "Enter your current password to change your sign-in email.");
                }
                else if (!await _userManager.CheckPasswordAsync(user, input.CurrentPassword))
                {
                    ModelState.AddModelError(nameof(input.CurrentPassword), "The current password is incorrect.");
                }
            }

            if (!ModelState.IsValid)
                return View(nameof(Index), CreateViewModel(user, input));

            user.FullName = input.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();

            if (emailChanged)
            {
                user.Email = email;
                user.UserName = email;
                user.SecurityStamp = Guid.NewGuid().ToString();
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(nameof(Index), CreateViewModel(user, input));
            }

            if (emailChanged)
                await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(MaxPhotoBytes + 64 * 1024)]
        public async Task<IActionResult> UploadPhoto(IFormFile? profilePhoto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (profilePhoto == null || profilePhoto.Length == 0)
                ModelState.AddModelError(nameof(ProfileViewModel.ProfilePhoto), "Choose a photo to upload.");
            else if (profilePhoto.Length > MaxPhotoBytes)
                ModelState.AddModelError(nameof(ProfileViewModel.ProfilePhoto), "Choose an image no larger than 5 MB.");

            if (!ModelState.IsValid)
                return View(nameof(Index), CreateViewModel(user));

            var newPhotoFileName = await SavePhotoAsync(profilePhoto!);
            if (newPhotoFileName == null)
                return View(nameof(Index), CreateViewModel(user));

            var previousPhotoFileName = user.ProfilePhotoFileName;
            user.ProfilePhotoFileName = newPhotoFileName;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                user.ProfilePhotoFileName = previousPhotoFileName;
                DeletePhoto(newPhotoFileName);
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(nameof(Index), CreateViewModel(user));
            }

            if (!string.IsNullOrWhiteSpace(previousPhotoFileName))
                DeletePhoto(previousPhotoFileName);

            TempData["Success"] = "Your profile photo has been updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePhoto()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (string.IsNullOrWhiteSpace(user.ProfilePhotoFileName))
            {
                TempData["Error"] = "You do not have a profile photo to remove.";
                return RedirectToAction(nameof(Index));
            }

            var previousPhotoFileName = user.ProfilePhotoFileName;
            user.ProfilePhotoFileName = null;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                user.ProfilePhotoFileName = previousPhotoFileName;
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(nameof(Index), CreateViewModel(user));
            }

            DeletePhoto(previousPhotoFileName);
            TempData["Success"] = "Your profile photo has been removed.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Photo()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (!TryGetPhotoPath(user.ProfilePhotoFileName, out var path) || !System.IO.File.Exists(path))
                return NotFound();

            Response.Headers["Cache-Control"] = "private, no-store";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return PhysicalFile(path, GetPhotoContentType(user.ProfilePhotoFileName!));
        }

        private async Task<string?> SavePhotoAsync(IFormFile photo)
        {
            var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!AllowedPhotoExtensions.Contains(extension, StringComparer.Ordinal))
            {
                ModelState.AddModelError(nameof(ProfileViewModel.ProfilePhoto), "Only JPG, PNG and WEBP images are allowed.");
                return null;
            }

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var directory = GetPhotoDirectory();
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, fileName);
            var saved = false;
            try
            {
                await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await photo.CopyToAsync(output);
                saved = true;
                return fileName;
            }
            finally
            {
                if (!saved && System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
        }

        private string GetPhotoDirectory() =>
            Path.Combine(_environment.ContentRootPath, "App_Data", "ProfilePhotos");

        private static bool IsPhotoFileName(string? fileName) =>
            fileName != null && Regex.IsMatch(
                fileName,
                @"\A[a-f0-9]{32}\.(?:jpg|jpeg|png|webp)\z",
                RegexOptions.CultureInvariant);

        private bool TryGetPhotoPath(string? fileName, out string path)
        {
            path = string.Empty;
            if (!IsPhotoFileName(fileName))
                return false;

            path = Path.Combine(GetPhotoDirectory(), fileName!);
            return true;
        }

        private static string GetPhotoContentType(string fileName) =>
            Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

        private void DeletePhoto(string fileName)
        {
            if (!IsPhotoFileName(fileName))
                return;

            try
            {
                var path = Path.Combine(GetPhotoDirectory(), fileName);
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
            catch (IOException exception)
            {
                _logger.LogWarning(exception, "Unable to remove replaced profile photo {PhotoFileName}.", fileName);
            }
            catch (UnauthorizedAccessException exception)
            {
                _logger.LogWarning(exception, "Unable to remove replaced profile photo {PhotoFileName}.", fileName);
            }
        }

        private ProfileViewModel CreateViewModel(ApplicationUser user, ProfileViewModel? input = null)
        {
            var model = input ?? new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber
            };

            model.CurrentPhotoUrl = IsPhotoFileName(user.ProfilePhotoFileName)
                ? Url.Action(nameof(Photo), "Profile", new { version = user.ProfilePhotoFileName })
                : null;
            return model;
        }
    }
}
