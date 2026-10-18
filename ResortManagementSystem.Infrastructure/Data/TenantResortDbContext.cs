using Microsoft.EntityFrameworkCore;
using ResortManagementSystem.Domain.Entities;

namespace ResortManagementSystem.Infrastructure.Data
{
    public class TenantResortDbContext : DbContext
    {
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Reservation> Reservations => Set<Reservation>();
        public DbSet<Staff> Staff => Set<Staff>();
        public DbSet<Promotion> Promotions => Set<Promotion>();
        public DbSet<Feedback> Feedbacks => Set<Feedback>();
        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<EmailMessage> EmailOutbox => Set<EmailMessage>();   // NEW: promo email outbox

        public TenantResortDbContext(DbContextOptions<TenantResortDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Product>(entity =>
            {
                entity.HasKey(x => x.ProductId);
                entity.Property(x => x.ProductCode).HasMaxLength(50).IsRequired();
                entity.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
                entity.HasIndex(x => x.ProductCode).IsUnique();
            });

            builder.Entity<Customer>(entity =>
            {
                entity.HasKey(x => x.CustomerId);
                entity.Property(x => x.FullName).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
                entity.HasIndex(x => x.Email).IsUnique();
            });
            builder.Entity<Reservation>(entity =>
            {
                entity.HasKey(x => x.ReservationId);
                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
                entity.Property(x => x.TotalAmount).HasPrecision(18, 2);

                entity.HasOne(x => x.Customer)
                      .WithMany()
                      .HasForeignKey(x => x.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Product)
                      .WithMany()
                      .HasForeignKey(x => x.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<Staff>(entity =>
            {
                entity.HasKey(x => x.StaffId);
                entity.Property(x => x.FullName).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Role).HasMaxLength(20).IsRequired();
                entity.HasIndex(x => x.Email).IsUnique();
            });
            builder.Entity<Promotion>(entity =>
            {
                entity.HasKey(x => x.PromotionId);
                entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
                entity.Property(x => x.Description).HasMaxLength(200).IsRequired();
                entity.Property(x => x.DiscountPercent).HasPrecision(5, 2);
                entity.HasIndex(x => x.Code).IsUnique();
            });

            builder.Entity<Feedback>(entity =>
            {
                entity.HasKey(x => x.FeedbackId);
                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();

                entity.HasOne(x => x.Customer)
                      .WithMany()
                      .HasForeignKey(x => x.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<Branch>(entity =>
            {
                entity.HasKey(x => x.BranchId);
                entity.Property(x => x.BranchName).HasMaxLength(150).IsRequired();
                entity.Property(x => x.Location).HasMaxLength(200);
            });

            builder.Entity<EmailMessage>(entity =>
            {
                entity.ToTable("EmailOutbox");
                entity.HasKey(x => x.EmailMessageId);
                entity.Property(x => x.ToEmail).HasMaxLength(200).IsRequired();
                entity.Property(x => x.ToName).HasMaxLength(200);
                entity.Property(x => x.Subject).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Body).IsRequired();
                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
                entity.Property(x => x.LastError).HasMaxLength(500);
                entity.HasIndex(x => x.Status);
                entity.HasIndex(x => x.PromotionId);
            });
        }
    }
}