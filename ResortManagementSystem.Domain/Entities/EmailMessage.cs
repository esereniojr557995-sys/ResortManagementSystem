using System.ComponentModel.DataAnnotations;

namespace ResortManagementSystem.Domain.Entities
{
    // One row per email: it is saved first (the outbox), then sent in the background.
    // If the mail server can't be reached the row stays Failed and can be retried later.
    public class EmailMessage
    {
        public int EmailMessageId { get; set; }
        public int? PromotionId { get; set; }
        public int? CustomerId { get; set; }

        [Required, MaxLength(200)] public string ToEmail { get; set; } = "";
        [MaxLength(200)] public string ToName { get; set; } = "";
        [Required, MaxLength(200)] public string Subject { get; set; } = "";
        [Required] public string Body { get; set; } = "";

        [MaxLength(20)] public string Status { get; set; } = "Pending";   // Pending | Sent | Failed
        public int Attempts { get; set; }
        [MaxLength(500)] public string? LastError { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SentAt { get; set; }
    }
}