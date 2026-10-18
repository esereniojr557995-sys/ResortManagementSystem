namespace ResortManagementSystem.Domain.Entities
{
    public class CompanyDatabase
    {
        public int CompanyDatabaseId { get; set; }
        public int CompanyId { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        // NEW — links this tenant's database to a named credential set
        // in appsettings.json (see Section 6)
        public string CredentialKey { get; set; } = string.Empty;

        public Company? Company { get; set; }
    }
}

