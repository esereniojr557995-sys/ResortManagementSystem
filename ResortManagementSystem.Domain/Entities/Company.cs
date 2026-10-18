namespace ResortManagementSystem.Domain.Entities
{
    public class Company
    {
        public int CompanyId { get; set; }
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string SubscriptionTier { get; set; } = "Micro";
        public bool IsSubscriptionActive { get; set; } = true;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Device> Devices { get; set; } = new List<Device>();
    }
}