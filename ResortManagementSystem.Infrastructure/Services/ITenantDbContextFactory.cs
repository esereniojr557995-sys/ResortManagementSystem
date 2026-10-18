using ResortManagementSystem.Infrastructure.Data;

namespace ResortManagementSystem.Infrastructure.Services
{
    public interface ITenantDbContextFactory
    {
        Task<TenantResortDbContext> CreateAsync(int companyId);
    }
}