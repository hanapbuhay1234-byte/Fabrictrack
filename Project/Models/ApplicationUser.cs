using Microsoft.AspNetCore.Identity;

namespace FabriTrack.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
    }

    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Staff = "Staff";
    }
}