using FabriTrack.Models;
using FabriTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FabriTrack.Controllers
{
    // Lets users update their profile and manage their private profile photo.
    [Authorize(Roles = Roles.Admin + "," + Roles.Staff)]
    public class ProfileController : Controller
    {
        private const long MaxPhotoBytes = 5 * 1024 * 1024;
        private static readonly string[] AllowedPhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IImageStorage _imageStorage;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IImageStorage imageStorage,
            ILogger<ProfileController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _imageStorage = imageStorage;
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
                // Ask for the current password before changing the sign-in email.
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

            // The email is also the username, so update both and refresh the sign-in security.
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
            else if (!AllowedPhotoExtensions.Contains(Path.GetExtension(profilePhoto.FileName), StringComparer.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(ProfileViewModel.ProfilePhoto), "Only JPG, PNG and WEBP images are allowed.");

            if (!ModelState.IsValid)
                return View(nameof(Index), CreateViewModel(user));

            string newPhotoFileName;
            try
            {
                newPhotoFileName = await _imageStorage.SaveAsync("profiles", profilePhoto!);
            }
            catch (ImageStorageException exception)
            {
                _logger.LogError(exception, "Unable to store a profile photo for user {UserId}.", user.Id);
                ModelState.AddModelError(nameof(ProfileViewModel.ProfilePhoto), "The photo could not be uploaded. Please try again.");
                return View(nameof(Index), CreateViewModel(user));
            }

            var previousPhotoFileName = user.ProfilePhotoFileName;
            user.ProfilePhotoFileName = newPhotoFileName;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                // Remove the new photo if the account update did not save.
                user.ProfilePhotoFileName = previousPhotoFileName;
                await DeletePhotoAsync(newPhotoFileName);
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(nameof(Index), CreateViewModel(user));
            }

            var oldPhotoCleanupFailed = !string.IsNullOrWhiteSpace(previousPhotoFileName) &&
                !await DeletePhotoAsync(previousPhotoFileName);

            TempData["Success"] = "Your profile photo has been updated.";
            if (oldPhotoCleanupFailed)
                TempData["Error"] = "Your profile photo was updated, but its previous file could not be removed from storage.";
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

            var cleanupFailed = !await DeletePhotoAsync(previousPhotoFileName);
            TempData["Success"] = "Your profile photo has been removed.";
            if (cleanupFailed)
                TempData["Error"] = "Your profile photo was removed, but its file could not be deleted from storage.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Photo()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();
            if (string.IsNullOrWhiteSpace(user.ProfilePhotoFileName))
                return NotFound();

            var photo = await _imageStorage.OpenReadAsync(user.ProfilePhotoFileName);
            if (photo == null)
                return NotFound();

            Response.Headers["Cache-Control"] = "private, no-store";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(photo.Content, photo.ContentType);
        }

        [HttpGet, Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> StaffPhoto(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null ||
                !await _userManager.IsInRoleAsync(user, Roles.Staff) ||
                await _userManager.IsInRoleAsync(user, Roles.Admin))
                return NotFound();
            if (string.IsNullOrWhiteSpace(user.ProfilePhotoFileName))
                return NotFound();

            var photo = await _imageStorage.OpenReadAsync(user.ProfilePhotoFileName);
            if (photo == null)
                return NotFound();

            Response.Headers["Cache-Control"] = "private, no-store";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(photo.Content, photo.ContentType);
        }

        private async Task<bool> DeletePhotoAsync(string fileName)
        {
            try
            {
                await _imageStorage.DeleteAsync(fileName);
                return true;
            }
            catch (ImageStorageException exception)
            {
                _logger.LogWarning(exception, "Unable to remove replaced profile photo {PhotoFileName}.", fileName);
                return false;
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

            model.CurrentPhotoUrl = !string.IsNullOrWhiteSpace(user.ProfilePhotoFileName)
                ? Url.Action(nameof(Photo), "Profile", new { version = user.ProfilePhotoFileName })
                : null;
            return model;
        }
    }
}
