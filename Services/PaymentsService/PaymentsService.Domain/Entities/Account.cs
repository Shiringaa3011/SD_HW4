using PaymentsService.Domain.ValueTypes;

namespace PaymentsService.Domain.Entities
{
    public class Account
    {
        public Guid UserId { get; }
        public Money Balance { get; private set; }
        public int Version { get; private set; } = 1;

        private Account(Guid userId, Money initialBalance)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User id is required", nameof(userId));
            }

            Balance = initialBalance ?? throw new ArgumentNullException(nameof(initialBalance));
            UserId = userId;
        }

        private Account(
            Guid userId,
            Money balance,
            int version)
        {
            UserId = userId;
            Balance = balance;
            Version = version;
        }

        public static Account Create(Guid userId)
        {
            return new(userId, Money.Create(0));
        }

        public static Account Recreate(
            Guid userId,
            Money balance,
            int version)
        {
            return userId == Guid.Empty
                ? throw new ArgumentException("User id is required", nameof(userId))
                : balance.Amount < 0
                ? throw new ArgumentException("Balance cannot be negative", nameof(balance))
                : version < 1
                ? throw new ArgumentException("Version must be positive", nameof(version))
                : new Account(userId, balance, version);
        }

        public void TopUp(Money amount)
        {
            Balance = Balance.Add(amount);
            ++Version;
        }

        public void Withdraw(Money amount)
        {
            if (!CanWithdraw(amount))
            {
                throw new ArgumentException("Insufficient funds to be debited", nameof(amount));
            }
            
            Balance = Balance.Subtract(amount);
            ++Version;
        }

        public bool CanWithdraw(Money amount)
        {
            return Balance.CanAfford(amount);
        }
    }

}
