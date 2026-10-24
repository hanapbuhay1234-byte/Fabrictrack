using System.ComponentModel.DataAnnotations;

namespace FabriTrack.Models
{
    public class AuditLog
    {
        public long AuditLogId { get; set; }

        [StringLength(450)]
        public string? ActorUserId { get; set; }

        [Required, StringLength(256)]
        public string ActorName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string EntityType { get; set; } = string.Empty;

        [StringLength(450)]
        public string? EntityId { get; set; }

        [StringLength(256)]
        public string? EntityName { get; set; }

        [StringLength(2000)]
        public string? Details { get; set; }

        public DateTime OccurredAt { get; set; }
    }
}
