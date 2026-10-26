using Microsoft.AspNetCore.Identity;

namespace FabriTrack.Models
{
    // Stores the staff details that Identity does not provide by itself.
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? ProfilePhotoFileName { get; set; }
        // Inactive accounts are not allowed to sign in.
        public bool IsActive { get; set; } = true;
        // New staff must replace their temporary password before using the app.
        public bool RequirePasswordChange { get; set; }
    }

    // These role names are used throughout the app to check access.
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Staff = "Staff";
    }
}