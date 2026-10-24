using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FabriTrack.Models
{
    public class ProfileViewModel
    {
        [Required, StringLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(256)]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Phone, StringLength(32)]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string? CurrentPassword { get; set; }

        [Display(Name = "Profile photo")]
        public IFormFile? ProfilePhoto { get; set; }

        public string? CurrentPhotoUrl { get; set; }
    }
}
