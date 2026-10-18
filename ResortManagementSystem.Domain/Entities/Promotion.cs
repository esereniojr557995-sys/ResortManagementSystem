using System.ComponentModel.DataAnnotations;

namespace ResortManagementSystem.Domain.Entities
{
    public class Promotion
    {
        public int PromotionId { get; set; }

        [Required, MaxLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        public decimal DiscountPercent { get; set; }  

        public bool IsActive { get; set; } = true;
        public string EligibleCategory { get; set; } = "All";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsArchived { get; set; } = false;
    }
}