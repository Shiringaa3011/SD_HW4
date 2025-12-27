using PaymentsService.Application.Dtos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentsService.Infrastructure.Persistence.Entities
{
    [Table("outbox_messages")]
    public class OutboxMessageDbModel
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("message_id")]
        [Required]
        public Guid MessageId { get; set; }

        [Column("correlation_id")]
        [Required]
        [MaxLength(255)]
        public string CorrelationId { get; set; } = null!;

        [Column("type")]
        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = null!;

        [Column("body")]
        [Required]
        public string Body { get; set; } = null!;

        [Column("topic")]
        [Required]
        [MaxLength(100)]
        public string Topic { get; set; } = null!;

        [Column("status")]
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        [Column("retry_count")]
        [Required]
        public int RetryCount { get; set; }

        [Column("created_at")]
        [Required]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("sent_at")]
        public DateTimeOffset? SentAt { get; set; }

        [Column("failed_at")]
        public DateTimeOffset? FailedAt { get; set; }

        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        [Column("version")]
        [Required]
        [ConcurrencyCheck]
        public int Version { get; set; } = 0;

        public static OutboxMessageDbModel FromDomain(Application.Dtos.OutboxMessage message)
        {
            return new OutboxMessageDbModel
            {
                Id = message.Id,
                MessageId = message.MessageId,
                CorrelationId = message.CorrelationId,
                Type = message.Type,
                Body = message.Body,
                Topic = message.Topic,
                Status = "Pending",
                RetryCount = 0,
                CreatedAt = message.CreatedAt,
                SentAt = null,
                FailedAt = null,
                ErrorMessage = null,
                Version = 0
            };
        }

        public Application.Dtos.OutboxMessage ToDomainEntity()
        {
            OutboxMessage message = new(
                messageId: MessageId,
                correlationId: CorrelationId,
                type: Type,
                body: Body,
                topic: Topic,
                createdAt: CreatedAt,
                id: Id
            );
            message.MarkAsSent();
            return message;
        }
    }
}