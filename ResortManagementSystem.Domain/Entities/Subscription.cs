namespace ResortManagementSystem.Domain.Entities
{
    public class Subscription
    {
        public int SubscriptionId { get; set; }
        public int CompanyId { get; set; }
        public string Tier { get; set; } = "Micro";
        public decimal PricePerMonth { get; set; }
        public int Months { get; set; } = 1;
        public decimal TotalPrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "Active";      // Active | Superseded

        // Filled in when the company's Admin accepts the Terms
        public int? TermsId { get; set; }
        public int? TermsVersion { get; set; }
        public string? AcceptedBy { get; set; }
        public DateTime? AcceptedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}