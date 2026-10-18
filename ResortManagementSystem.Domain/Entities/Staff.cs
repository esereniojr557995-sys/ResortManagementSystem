using System.ComponentModel.DataAnnotations;

namespace ResortManagementSystem.Domain.Entities
{
    public class Staff
    {
        public int StaffId { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? PhoneNumber { get; set; }

        [Required, MaxLength(20)]
        public string Role { get; set; } = "Staff"; // Staff, Manager, Admin

        public bool IsActive { get; set; } = true;

        public DateTime HireDate { get; set; } = DateTime.UtcNow;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsArchived { get; set; } = false;

        // The branch this person works at; null = not assigned (Admins are never assigned)
        public int? BranchId { get; set; }
    }
}