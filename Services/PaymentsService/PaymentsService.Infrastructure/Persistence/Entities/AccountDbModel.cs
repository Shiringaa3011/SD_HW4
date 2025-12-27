using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using PaymentsService.Domain.ValueTypes;

namespace PaymentsService.Infrastructure.Persistence.Entities
{
    [Table("accounts")]
    public class AccountDbModel
    {
        [Key]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("balance_amount")]
        [Required]
        [Precision(18, 2)]
        public decimal BalanceAmount { get; set; }

        [Column("balance_currency")]
        [Required]
        [MaxLength(3)]
        public string BalanceCurrency { get; set; } = "RUB";

        [Column("version")]
        [Required]
        [ConcurrencyCheck]
        public int Version { get; set; } = 1;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public static AccountDbModel FromDomain(Domain.Entities.Account account)
        {
            return new AccountDbModel
            {
                UserId = account.UserId,
                BalanceAmount = account.Balance.Amount,
                BalanceCurrency = account.Balance.Currency,
                Version = account.Version,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        public Domain.Entities.Account ToDomainEntity()
        {
            Money money = Domain.ValueTypes.Money.Create(BalanceAmount, BalanceCurrency);

            return Domain.Entities.Account.Recreate(UserId, money, Version);
        }
    }
}
