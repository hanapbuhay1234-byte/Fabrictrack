using System.ComponentModel.DataAnnotations;

namespace FabriTrack.Models
{
    // Records who changed something in the app and what was changed.
    public class AuditLog
    {
        public long AuditLogId { get; set; }

        // This can be empty for older records or changes made by the system.
        [StringLength(450)]
        public string? ActorUserId { get; set; }

        // Saved names keep the history readable even if a person or record is renamed.
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
        // Holds extra change details in JSON format.
        public string? Details { get; set; }

        // The time the change happened, saved in UTC.
        public DateTime OccurredAt { get; set; }
    }
}
