using System.ComponentModel.DataAnnotations;

namespace FabriTrack.Models
{
    // Stores a customer's contact details and links them to their sales.
    public class Customer
    {
        public int CustomerId { get; set; }

        [Required, StringLength(150)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(20)]
        public string? ContactNumber { get; set; }

        [StringLength(150), EmailAddress]
        public string? Email { get; set; }

        [StringLength(250)]
        public string? Address { get; set; }

        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    }
}