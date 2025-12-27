using System;

namespace OrderService.Domain.ValueTypes
{
    public record Money
    {
        public decimal Amount { get; }
        public string Currency { get; }

        private Money(decimal amount, string currency = "RUB")
        {
            if (amount < 0)
            {
                throw new ArgumentException("Сумма не может быть отрицательной", nameof(amount));
            }

            Amount = amount;
            Currency = currency;
        }

        public static Money Create(decimal amount, string currency = "RUB")
        {
            return new Money(amount, currency);
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
