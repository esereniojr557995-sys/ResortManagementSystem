using System.ComponentModel.DataAnnotations;

namespace ResortManagementSystem.Domain.Entities
{
    public class Branch
    {
        public int BranchId { get; set; }

        [Required, MaxLength(150)]
        public string BranchName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Location { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public bool IsArchived { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // The Staff member (role Manager) who runs this branch; null = none chosen yet
        public int? ManagerStaffId { get; set; }
    }
}