using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ResortManagementSystem.Infrastructure.Data;

namespace ResortManagementSystem.Infrastructure.Services
{
    public class TenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly ITenantDatabaseResolver _resolver;
        private readonly IConfiguration _configuration;

        public TenantDbContextFactory(
            ITenantDatabaseResolver resolver,
            IConfiguration configuration)
        {
            _resolver = resolver;
            _configuration = configuration;
        }

        public async Task<TenantResortDbContext> CreateAsync(int companyId)
        {
            var databaseInfo = await _resolver.GetDatabaseInfoAsync(companyId);

            string connectionString;

            if (IsLocalServer(databaseInfo.ServerName))
            {
                // Local SQL Server / LocalDB: Windows login, no password needed
                connectionString =
                    $"Server={databaseInfo.ServerName};" +
                    $"Database={databaseInfo.DatabaseName};" +
                    "Trusted_Connection=True;" +
                    "TrustServerCertificate=True;" +
                    "MultipleActiveResultSets=True;";
            }
            else
            {
                // Cloud server: needs the TenantCredentials section in appsettings.json
                var userId = _configuration[
                    $"TenantCredentials:{databaseInfo.CredentialKey}:UserId"];

                var password = _configuration[
                    $"TenantCredentials:{databaseInfo.CredentialKey}:Password"];

                if (string.IsNullOrWhiteSpace(userId) ||
                    string.IsNullOrWhiteSpace(password))
                {
                    throw new InvalidOperationException(
                        $"Credentials not found for key '{databaseInfo.CredentialKey}'.");
                }

                connectionString =
                    $"Server={databaseInfo.ServerName};" +
                    $"Database={databaseInfo.DatabaseName};" +
                    $"User Id={userId};" +
                    $"Password={password};" +
                    "Encrypt=True;" +
                    "TrustServerCertificate=True;" +
                    "MultipleActiveResultSets=True;";
            }

            var options = new DbContextOptionsBuilder<TenantResortDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new TenantResortDbContext(options);
        }

        private static bool IsLocalServer(string serverName)
        {
            var s = serverName.Trim();
            return s.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase)
                || s.StartsWith("(local)", StringComparison.OrdinalIgnoreCase)
                || s.Equals(".", StringComparison.Ordinal)
                || s.StartsWith("localhost", StringComparison.OrdinalIgnoreCase);
        }
    }
}