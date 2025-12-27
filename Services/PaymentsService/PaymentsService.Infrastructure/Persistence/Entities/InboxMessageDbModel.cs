using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentsService.Infrastructure.Persistence.Entities
{
    [Table("inbox_messages")]
    public class InboxMessageDbModel
    {
        [Key]
        [Column("id")]
        [MaxLength(255)]
        public string Id { get; set; } = null!;

        [Column("order_id")]
        [Required]
        public Guid OrderId { get; set; }

        [Column("user_id")]
        [Required]
        public Guid UserId { get; set; }

        [Column("body")]
        [Required]
        public string Body { get; set; } = null!;

        [Column("message_type")]
        [Required]
        [MaxLength(50)]
        public string MessageType { get; set; } = null!;

        [Column("status")]
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        [Column("retry_count")]
        [Required]
        public int RetryCount { get; set; }

        [Column("processor_id")]
        [MaxLength(255)]
        public string? ProcessorId { get; set; }

        [Column("locked_at")]
        public DateTimeOffset? LockedAt { get; set; }

        [Column("received_at")]
        [Required]
        public DateTimeOffset ReceivedAt { get; set; }

        [Column("processed_at")]
        public DateTimeOffset? ProcessedAt { get; set; }

        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        [Column("version")]
        [Required]
        [ConcurrencyCheck]
        public int Version { get; set; } = 0;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public static InboxMessageDbModel FromDomain(Application.Ports.InboxMessage message)
        {
            return new InboxMessageDbModel
            {
                Id = message.Id,
                OrderId = message.OrderId,
                UserId = message.UserId,
                Body = message.Body,
                MessageType = message.MessageType,
                Status = message.Status.ToString(),
                RetryCount = message.RetryCount,
                ProcessorId = message.ProcessorId,
                LockedAt = message.LockedAt,
                ReceivedAt = message.ReceivedAt,
                ProcessedAt = message.ProcessedAt,
                ErrorMessage = null,
                Version = 0,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }

        public Application.Ports.InboxMessage ToDomainEntity()
        {
            return new Application.Ports.InboxMessage(
                Id: Id,
                OrderId: OrderId,
                UserId: UserId,
                Body: Body,
                MessageType: MessageType,
                Status: Enum.Parse<Application.Ports.InboxMessageStatus>(Status),
                RetryCount: RetryCount,
                ProcessorId: ProcessorId,
                LockedAt: LockedAt,
                ReceivedAt: ReceivedAt,
                ProcessedAt: ProcessedAt
            );
        }
    }
}