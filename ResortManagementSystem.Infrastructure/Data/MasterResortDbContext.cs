using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ResortManagementSystem.Domain.Entities;

namespace ResortManagementSystem.Infrastructure.Data
{
    public class MasterResortDbContext : IdentityDbContext
    {
        public DbSet<Company> Companies => Set<Company>();
        public DbSet<CompanyDatabase> CompanyDatabases => Set<CompanyDatabase>();
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<UserAccount> UserAccounts => Set<UserAccount>();   // NEW
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<TermsAndConditions> TermsVersions => Set<TermsAndConditions>();

        public MasterResortDbContext(DbContextOptions<MasterResortDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Company>(entity =>
            {
                entity.HasKey(x => x.CompanyId);
                entity.Property(x => x.CompanyCode).HasMaxLength(50).IsRequired();
                entity.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
                entity.HasIndex(x => x.CompanyCode).IsUnique();
            });

            builder.Entity<CompanyDatabase>(entity =>
            {
                entity.HasKey(x => x.CompanyDatabaseId);
                entity.Property(x => x.ServerName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.DatabaseName).HasMaxLength(200).IsRequired();
                entity.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Device>(entity =>
            {
                entity.HasKey(x => x.DeviceId);
                entity.Property(x => x.DeviceCode).HasMaxLength(50).IsRequired();
                entity.Property(x => x.DeviceName).HasMaxLength(200).IsRequired();
                entity.HasOne(x => x.Company)
                    .WithMany(x => x.Devices)
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(x => new { x.CompanyId, x.DeviceCode }).IsUnique();
            });

            // NEW: login accounts, one email maps to exactly one company
            builder.Entity<UserAccount>(entity =>
            {
                entity.HasKey(x => x.UserAccountId);
                entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
                entity.Property(x => x.PasswordHash).IsRequired();
                entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Role).HasMaxLength(20).IsRequired();
                entity.HasIndex(x => x.Email).IsUnique();
                entity.HasOne<Company>()
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<Subscription>(entity =>
            {
                entity.HasKey(x => x.SubscriptionId);
                entity.Property(x => x.Tier).HasMaxLength(20).IsRequired();
                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
                entity.Property(x => x.AcceptedBy).HasMaxLength(200);
                entity.Property(x => x.PricePerMonth).HasPrecision(18, 2);
                entity.Property(x => x.TotalPrice).HasPrecision(18, 2);
                entity.HasIndex(x => new { x.CompanyId, x.Status });
                entity.HasOne<Company>()
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<TermsAndConditions>(entity =>
            {
                entity.HasKey(x => x.TermsId);
                entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Body).IsRequired();
                entity.HasIndex(x => x.Version).IsUnique();
            });
        }
    }
}