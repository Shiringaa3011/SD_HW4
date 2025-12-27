using PaymentsService.Domain.Enums;
using PaymentsService.Domain.ValueTypes;

namespace PaymentsService.Domain.Entities
{
    public class Payment
    {
        public Guid Id { get; }
        public Guid OrderId { get; }
        public Guid UserId { get; }
        public Money Amount { get; }
        public PaymentStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset? CompletedAt { get; private set; }
        public int Version { get; private set; } = 1;

        private Payment(Guid id, Guid orderId, Guid userId, Money amount)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Payment id required", nameof(id));
            }

            if (orderId == Guid.Empty)
            {
                throw new ArgumentException("Order id required", nameof(orderId));
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User id required", nameof(userId));
            }

            Id = id;
            OrderId = orderId;
            UserId = userId;
            Amount = amount ?? throw new ArgumentNullException(nameof(amount));
            Status = PaymentStatus.Pending;
            CreatedAt = DateTimeOffset.UtcNow;
        }

        private Payment(
        Guid id,
        Guid orderId,
        Guid userId,
        Money amount,
        PaymentStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt,
        int version)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Payment id required", nameof(id));
            }

            if (orderId == Guid.Empty)
            {
                throw new ArgumentException("Order id required", nameof(orderId));
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User id required", nameof(userId));
            }

            Id = id;
            OrderId = orderId;
            UserId = userId;
            Amount = amount;
            Status = status;
            CreatedAt = createdAt;
            CompletedAt = completedAt;
            Version = version;
        }

        public static Payment Create(Guid orderId, Guid userId, Money amount)
        {
            return new Payment(Guid.NewGuid(), orderId, userId, amount);
        }

        public static Payment Restore(
        Guid id,
        Guid orderId,
        Guid userId,
        Money amount,
        PaymentStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt,
        int version)
        {
            return new Payment(
                id: id,
                orderId: orderId,
                userId: userId,
                amount: amount,
                status: status,
                createdAt: createdAt,
                completedAt: completedAt,
                version: version);
        }

        public void MarkSuccess()
        {
            if (Status == PaymentStatus.Success)
            {
                return;
            }

            if (Status == PaymentStatus.Failed)
            {
                throw new InvalidOperationException("Failed payment cannot be marked successful");
            }

            Status = PaymentStatus.Success;
            ++Version;
            CompletedAt = DateTimeOffset.UtcNow;
        }

        public void MarkFailed()
        {
            if (Status == PaymentStatus.Failed)
            {
                return;
            }

            if (Status == PaymentStatus.Success)
            {
                throw new InvalidOperationException("Successful payment cannot be failed");
            }

            Status = PaymentStatus.Failed;
            ++Version;
            CompletedAt = DateTimeOffset.UtcNow;
        }
    }

}
