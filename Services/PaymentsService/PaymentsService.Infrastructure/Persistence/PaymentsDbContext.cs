using Microsoft.EntityFrameworkCore;
using PaymentsService.Infrastructure.Persistence.Entities;

namespace PaymentsService.Infrastructure.Persistence
{
    public class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
    {
        public DbSet<AccountDbModel> Accounts { get; set; }
        public DbSet<PaymentDbModel> Payments { get; set; }
        public DbSet<WithdrawalDbModel> Withdrawals { get; set; }
        public DbSet<InboxMessageDbModel> InboxMessages { get; set; }
        public DbSet<OutboxMessageDbModel> OutboxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.Entity<AccountDbModel>(entity =>
            {
                _ = entity.HasKey(e => e.UserId);
                _ = entity.Property(e => e.BalanceAmount)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                _ = entity.Property(e => e.BalanceCurrency)
                    .HasMaxLength(3)
                    .IsRequired()
                    .HasDefaultValue("RUB");
                _ = entity.Property(e => e.Version)
                    .IsConcurrencyToken()
                    .IsRequired()
                    .HasDefaultValue(1);
                _ = entity.ToTable(t => t.HasCheckConstraint("CK_Accounts_BalancePositive", "balance_amount >= 0"));
            });

            _ = modelBuilder.Entity<PaymentDbModel>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.HasIndex(e => e.OrderId).IsUnique();
                _ = entity.HasIndex(e => e.UserId);
                _ = entity.HasIndex(e => e.Status);
                _ = entity.Property(e => e.AmountAmount)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                _ = entity.Property(e => e.AmountCurrency)
                    .HasMaxLength(3)
                    .IsRequired()
                    .HasDefaultValue("RUB");
                _ = entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .IsRequired()
                    .HasDefaultValue("Pending");
                _ = entity.Property(e => e.Version)
                    .IsConcurrencyToken()
                    .IsRequired()
                    .HasDefaultValue(1);
                _ = entity.ToTable(t => t.HasCheckConstraint("CK_Payments_Status",
                    "status IN ('Pending', 'Success', 'Failed')"));
                _ = entity.ToTable(t => t.HasCheckConstraint("CK_Payments_AmountPositive",
                    "amount_amount > 0"));

                // Связь с Account
                _ = entity.HasOne<AccountDbModel>()
                    .WithMany()
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            _ = modelBuilder.Entity<WithdrawalDbModel>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.HasIndex(e => e.PaymentId).IsUnique();
                _ = entity.Property(e => e.AmountAmount)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                _ = entity.Property(e => e.AmountCurrency)
                    .HasMaxLength(3)
                    .IsRequired()
                    .HasDefaultValue("RUB");
                _ = entity.ToTable(t => t.HasCheckConstraint("CK_Withdrawals_AmountPositive",
                    "amount_amount > 0"));

                // Связь с Payment
                _ = entity.HasOne<PaymentDbModel>()
                    .WithMany()
                    .HasForeignKey(w => w.PaymentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            _ = modelBuilder.Entity<InboxMessageDbModel>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.Property(e => e.Id).HasMaxLength(255);
                _ = entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .IsRequired()
                    .HasDefaultValue("Pending");
                _ = entity.Property(e => e.MessageType).HasMaxLength(50).IsRequired();
                _ = entity.HasIndex(e => new { e.Status, e.CreatedAt })
                    .HasFilter("status IN ('Pending', 'Processing')");
                _ = entity.HasIndex(e => e.OrderId);
                _ = entity.HasIndex(e => e.UserId);
                _ = entity.ToTable(t => t.HasCheckConstraint("CK_InboxMessages_Status",
                    "status IN ('Pending', 'Processing', 'Processed', 'Failed', 'DeadLetter')"));
                _ = entity.ToTable(t => t.HasCheckConstraint("CK_InboxMessages_MaxRetries",
                    "retry_count <= 10"));

                // Связь с Account
                _ = entity.HasOne<AccountDbModel>()
                    .WithMany()
                    .HasForeignKey(m => m.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            _ = modelBuilder.Entity<OutboxMessageDbModel>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.HasIndex(e => e.MessageId).IsUnique();
                _ = entity.HasIndex(e => e.CorrelationId);
                _ = entity.HasIndex(e => new { e.Status, e.CreatedAt });
                _ = entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .IsRequired()
                    .HasDefaultValue("Pending");
                _ = entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
                _ = entity.Property(e => e.Topic).HasMaxLength(100).IsRequired();
                _ = entity.ToTable(t => t.HasCheckConstraint("CK_OutboxMessages_Status",
                    "status IN ('Pending', 'Sent', 'Failed')"));
                _ = entity.ToTable(t => t.HasCheckConstraint("CK_OutboxMessages_MaxRetries",
                    "retry_count <= 5"));
            });
        }
    }
}