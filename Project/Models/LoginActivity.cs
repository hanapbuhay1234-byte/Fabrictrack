using System.ComponentModel.DataAnnotations;

namespace FabriTrack.Models
{
    public class LoginActivity
    {
        public long LoginActivityId { get; set; }

        [StringLength(450)]
        public string? UserId { get; set; }

        [StringLength(256)]
        public string? AccountName { get; set; }

        [StringLength(256)]
        public string? AccountIdentifier { get; set; }

        [Required, StringLength(50)]
        public string EventType { get; set; } = string.Empty;

        [StringLength(45)]
        public string? IpAddress { get; set; }

        [StringLength(512)]
        public string? UserAgent { get; set; }

        public DateTime OccurredAt { get; set; }
    }
}
