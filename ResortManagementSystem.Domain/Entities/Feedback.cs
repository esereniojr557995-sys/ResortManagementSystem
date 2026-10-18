using System.ComponentModel.DataAnnotations;

namespace ResortManagementSystem.Domain.Entities
{
    public class Feedback
    {
        public int FeedbackId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [Required]
        public string Comments { get; set; } = string.Empty;

        public bool IsComplaint { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Open"; // Open, Resolved

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAt { get; set; }

        public Customer? Customer { get; set; }
    }
}