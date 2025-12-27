using PaymentsService.Domain.ValueTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace PaymentsService.Domain.Entities
{
    public class Withdrawal
    {
        public Guid Id { get; }
        public Guid PaymentId { get; }
        public Money Amount { get; }
        public bool Success { get; }
        public DateTimeOffset CreatedAt { get; }

        private Withdrawal(Guid id, Guid paymentId, Money amount, bool success)
        {
            Id = id;
            PaymentId = paymentId;
            Amount = amount;
            Success = success;
            CreatedAt = DateTimeOffset.UtcNow;
        }

        private Withdrawal(Guid id, Guid paymentId, Money amount, bool success, DateTimeOffset createdAt)
        {
            Id = id;
            PaymentId = paymentId;
            Amount = amount;
            Success = success;
            CreatedAt = createdAt;
        }
        public static Withdrawal Record(Guid paymentId, Money amount, bool success)
        {
            return new Withdrawal(Guid.NewGuid(), paymentId, amount, success);
        }

        public static Withdrawal Restore(
            Guid id,
            Guid paymentId,
            Money amount,
            bool success,
            DateTimeOffset createdAt)
        {
            return new Withdrawal(id, paymentId, amount, success, createdAt);
        }
    }
}
