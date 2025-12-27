using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Persistence.Repositories;
using OrderService.Infrastructure.Idempotency;

namespace OrderService.Infrastructure.Persistence
{
    public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
    {
        public DbSet<OrderEntity> Orders { get; set; }
        public DbSet<OutboxEntity> Outbox { get; set; }
        public DbSet<ProcessedMessage> ProcessedMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.Entity<OrderEntity>(entity =>
            {
                _ = entity.ToTable("orders", "public");
                _ = entity.HasKey(e => e.Id);

                // Маппинг столбцов
                _ = entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                _ = entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                _ = entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)").IsRequired();
                _ = entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
                _ = entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
                _ = entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
                _ = entity.Property(e => e.Version).HasColumnName("version").IsRequired().HasDefaultValue(1);
                _ = entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
                _ = entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

                // Индексы
                _ = entity.HasIndex(e => e.UserId).HasDatabaseName("idx_orders_user_id");
                _ = entity.HasIndex(e => e.Status).HasDatabaseName("idx_orders_status");

                // Оптимистическая блокировка
                _ = entity.Property(e => e.Version).IsConcurrencyToken();
            });

            _ = modelBuilder.Entity<OutboxEntity>(entity =>
            {
                _ = entity.ToTable("outbox_messages", "public");
                _ = entity.HasKey(e => e.Id);

                // Маппинг столбцов
                _ = entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                _ = entity.Property(e => e.Type).HasColumnName("type").HasMaxLength(100).IsRequired();
                _ = entity.Property(e => e.Data).HasColumnName("data").IsRequired();
                _ = entity.Property(e => e.Queue).HasColumnName("queue").HasMaxLength(100);
                _ = entity.Property(e => e.OrderId).HasColumnName("order_id");
                _ = entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
                _ = entity.Property(e => e.Processed).HasColumnName("processed").IsRequired().HasDefaultValue(false);
                _ = entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
                _ = entity.Property(e => e.RetryCount).HasColumnName("retry_count").IsRequired().HasDefaultValue(0);
                _ = entity.Property(e => e.Error).HasColumnName("error").HasMaxLength(1000);

                // Индексы
                _ = entity.HasIndex(e => new { e.Processed, e.CreatedAt })
                      .HasDatabaseName("idx_outbox_messages_processed");

                // Внешний ключ
                _ = entity.HasOne(e => e.Order)
                      .WithMany(o => o.OutboxMessages)
                      .HasForeignKey(e => e.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            _ = modelBuilder.Entity<ProcessedMessage>(entity =>
            {
                _ = entity.ToTable("processed_messages", "public");
                _ = entity.HasKey(e => e.Id);

                // Маппинг столбцов
                _ = entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                _ = entity.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(255).IsRequired();
                _ = entity.Property(e => e.Details).HasColumnName("details").IsRequired();
                _ = entity.Property(e => e.ProcessedAt).HasColumnName("processed_at").IsRequired();
                _ = entity.Property(e => e.MessageId).HasColumnName("message_id").HasMaxLength(100);
                _ = entity.Property(e => e.MessageType).HasColumnName("message_type").HasMaxLength(100);
                _ = entity.Property(e => e.CreatedAt).HasColumnName("created_at");

                // Индексы
                _ = entity.HasIndex(e => e.IdempotencyKey)
                      .IsUnique()
                      .HasDatabaseName("idx_processed_messages_key");
                _ = entity.HasIndex(e => e.MessageId);
                _ = entity.HasIndex(e => e.ProcessedAt);
            });
        }
    }
}