using System.ComponentModel.DataAnnotations;

namespace FabriTrack.Models
{
    public class QuickCustomerCreateViewModel
    {
        [Required, StringLength(150)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(20)]
        public string? ContactNumber { get; set; }

        [StringLength(150), EmailAddress]
        public string? Email { get; set; }
    }
}
