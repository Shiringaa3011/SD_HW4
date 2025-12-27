using OrderService.Domain.Enums;
using OrderService.Domain.ValueTypes;
using System;

namespace OrderService.Domain.Entities
{
    public class Order
    {
        public Guid Id { get; }
        public Guid UserId { get; }
        public Money Amount { get; }
        public OrderDescription Description { get; }
        public OrderStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; }
        public int Version { get; private set; }

        private Order(Guid id, Guid userId, Money amount, OrderDescription description, DateTimeOffset time)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Order id required", nameof(id));
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User id required", nameof(userId));
            }

            Id = id;
            UserId = userId;
            Amount = amount ?? throw new ArgumentNullException(nameof(amount));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Status = OrderStatus.New;
            if (time > DateTimeOffset.Now)
            {
                throw new ArgumentException();
            }
            CreatedAt = time;
            Version = 1;
        }

        private Order(
            Guid id,
            Guid userId,
            Money amount,
            OrderDescription description,
            DateTimeOffset createdAt,
            OrderStatus status,
            int version) : this(id, userId, amount, description, createdAt)
        {
            Status = status;
            Version = version;
        }

        public static Order Create(Guid userId, Money amount, OrderDescription description, DateTimeOffset time)
        {
            return new Order(Guid.NewGuid(), userId, amount, description, time);
        }

        public static Order Recreate(
            Guid id,
            Guid userId,
            Money amount,
            OrderDescription description,
            OrderStatus status,
            DateTimeOffset createdAt,
            int version)
        {
            Order order = new(id, userId, amount, description, createdAt, status, version);
            return order;
        }

        public void MarkFinished()
        {
            if (Status == OrderStatus.Finished)
            {
                return;
            }

            if (Status == OrderStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot finish cancelled order");
            }

            Status = OrderStatus.Finished;
            ++Version;
        }

        public void MarkCancelled()
        {
            if (Status == OrderStatus.Cancelled)
            {
                return;
            }

            if (Status == OrderStatus.Finished)
            {
                throw new InvalidOperationException("Cannot cancel finished order");
            }

            Status = OrderStatus.Cancelled;
            ++Version;
        }
    }
}
