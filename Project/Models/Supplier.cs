using System.ComponentModel.DataAnnotations;

namespace FabriTrack.Models
{
    public class Supplier
    {
        public int SupplierId { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(20)]
        public string? ContactNumber { get; set; }

        [StringLength(150), EmailAddress]
        public string? Email { get; set; }

        [StringLength(250)]
        public string? Address { get; set; }

        public ICollection<Fabric> Fabrics { get; set; } = new List<Fabric>();
    }
}