namespace ResortManagementSystem.Domain.Entities
{
    public class TermsAndConditions
    {
        public int TermsId { get; set; }
        public int Version { get; set; }
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public bool IsCurrent { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}