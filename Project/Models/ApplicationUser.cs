using Microsoft.AspNetCore.Identity;

namespace FabriTrack.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? ProfilePhotoFileName { get; set; }
        public bool IsActive { get; set; } = true;
        public bool RequirePasswordChange { get; set; }
    }

    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Staff = "Staff";
    }
}