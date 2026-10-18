// ResortManagementSystem.Domain/Entities/Customer.cs
using System.ComponentModel.DataAnnotations;

namespace ResortManagementSystem.Domain.Entities
{
    public class Customer
    {
        public int CustomerId { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? PhoneNumber { get; set; }
        public string CustomerCategory { get; set; } = "Regular";
        public string? Address { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsArchived { get; set; } = false;
    }
}