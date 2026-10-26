using System.ComponentModel.DataAnnotations;

namespace FabriTrack.Models
{
    // Records sign-ins and sign-outs separately from changes to business records.
    public class LoginActivity
    {
        public long LoginActivityId { get; set; }

        [StringLength(450)]
        public string? UserId { get; set; }

        [StringLength(256)]
        public string? AccountName { get; set; }

        [StringLength(256)]
        // May hold the account name used at sign-in, but never a password or security code.
        public string? AccountIdentifier { get; set; }

        [Required, StringLength(50)]
        public string EventType { get; set; } = string.Empty;

        [StringLength(45)]
        public string? IpAddress { get; set; }

        [StringLength(512)]
        public string? UserAgent { get; set; }

        // The event time is saved in UTC; old entries are cleaned up by the recorder.
        public DateTime OccurredAt { get; set; }
    }
}
