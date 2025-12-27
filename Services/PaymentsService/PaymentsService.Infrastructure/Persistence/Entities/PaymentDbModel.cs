using PaymentsService.Domain.Entities;
using PaymentsService.Domain.Enums;
using PaymentsService.Domain.ValueTypes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace PaymentsService.Infrastructure.Persistence.Entities
{
    [Table("payments")]
    public class PaymentDbModel
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("order_id")]
        [Required]
        public Guid OrderId { get; set; }

        [Column("user_id")]
        [Required]
        public Guid UserId { get; set; }

        [Column("amount_amount")]
        [Required]
        public decimal AmountAmount { get; set; }

        [Column("amount_currency")]
        [Required]
        [MaxLength(3)]
        public string AmountCurrency { get; set; } = "RUB";

        [Column("status")]
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        [Column("version")]
        [Required]
        [ConcurrencyCheck]
        public int Version { get; set; } = 1;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("completed_at")]
        public DateTimeOffset? CompletedAt { get; set; }

        public static PaymentDbModel FromDomain(Domain.Entities.Payment payment)
        {
            return new PaymentDbModel
            {
                Id = payment.Id,
                OrderId = payment.OrderId,
                UserId = payment.UserId,
                AmountAmount = payment.Amount.Amount,
                AmountCurrency = payment.Amount.Currency,
                Status = payment.Status.ToString(),
                Version = payment.Version,
                CreatedAt = payment.CreatedAt,
                CompletedAt = payment.CompletedAt
            };
        }

        public Domain.Entities.Payment ToDomainEntity()
        {
            Payment payment = Payment.Restore(
                id: Id,
                orderId: OrderId,
                userId: UserId,
                amount: Money.Create(AmountAmount, AmountCurrency),
                status: Enum.Parse<PaymentStatus>(Status),
                createdAt: CreatedAt,
                completedAt: CompletedAt,
                version: Version);
            return payment;
        }
    }
}