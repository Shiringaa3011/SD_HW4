namespace PaymentsService.Domain.ValueTypes
{
    public record Money
    {
        public decimal Amount { get; }
        public string Currency { get; } = "RUB";

        private Money(decimal amount, string currency = "RUB")
        {
            if (amount < 0)
            {
                throw new ArgumentException("Amount must be non-negative", nameof(amount));
            }

            Amount = amount;
            Currency = currency;
        }

        public static Money Create(decimal amount, string currency = "RUB")
        {
            return new(amount, currency);
        }

        public Money Add(Money other)
        {
            EnsureSameCurrency(other);
            return new Money(Amount + other.Amount);
        }

        public Money Subtract(Money other)
        {
            EnsureSameCurrency(other);
            return Amount < other.Amount ? throw new InvalidOperationException("Insufficient funds") : new Money(Amount - other.Amount);
        }

        public bool CanAfford(Money other)
        {
            return Currency == other.Currency && Amount >= other.Amount;
        }

        private void EnsureSameCurrency(Money other)
        {
            if (Currency != other.Currency)
            {
                throw new InvalidOperationException("Currency mismatch");
            }
        }

        public static implicit operator decimal(Money money)
        {
            return money.Amount;
        }

        public static implicit operator Money(decimal amount)
        {
            return Create(amount);
        }
    }

}
