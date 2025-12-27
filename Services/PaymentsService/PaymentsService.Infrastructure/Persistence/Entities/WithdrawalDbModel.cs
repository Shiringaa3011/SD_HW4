using PaymentsService.Domain.Entities;
using PaymentsService.Domain.ValueTypes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace PaymentsService.Infrastructure.Persistence.Entities
{
    [Table("withdrawals")]
    public class WithdrawalDbModel
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("payment_id")]
        [Required]
        public Guid PaymentId { get; set; }

        [Column("amount_amount")]
        [Required]
        public decimal AmountAmount { get; set; }

        [Column("amount_currency")]
        [Required]
        [MaxLength(3)]
        public string AmountCurrency { get; set; } = "RUB";

        [Column("success")]
        [Required]
        public bool Success { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public static WithdrawalDbModel FromDomain(Domain.Entities.Withdrawal withdrawal)
        {
            return new WithdrawalDbModel
            {
                Id = withdrawal.Id,
                PaymentId = withdrawal.PaymentId,
                AmountAmount = withdrawal.Amount.Amount,
                AmountCurrency = withdrawal.Amount.Currency,
                Success = withdrawal.Success,
                CreatedAt = withdrawal.CreatedAt
            };
        }

        public Domain.Entities.Withdrawal ToDomainEntity()
        {
            Money amount = Domain.ValueTypes.Money.Create(AmountAmount, AmountCurrency);

            return Withdrawal.Restore(
                id: Id,
                paymentId: PaymentId,
                amount: amount,
                success: Success,
                createdAt: CreatedAt);
        }
    }
}