using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using OrderService.Application.Ports;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Domain.ValueTypes;

namespace OrderService.Infrastructure.Persistence.Repositories
{
    public class OrderRepository(OrderDbContext context, ILogger<OrderRepository> logger) : IOrderRepository
    {
        private readonly OrderDbContext _context = context;
        private readonly ILogger<OrderRepository> _logger = logger;

        public async Task AddAsync(Order? order, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(order);

            OrderEntity orderEntity = MapToEntity(order);

            _ = await _context.Orders.AddAsync(orderEntity, ct);
            _ = await _context.SaveChangesAsync(ct);

            _logger.LogDebug("Order added. ID: {OrderId}", order.Id);
        }

        public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default)
        {
            OrderEntity? entity = await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            return entity == null ? null : MapToDomain(entity);
        }

        public async Task<(Order?, int)> GetByIdWithVersionAsync(Guid orderId, CancellationToken ct)
        {
            OrderEntity? entity = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            if (entity == null)
            {
                return (null, 0);
            }

            return (MapToDomain(entity), entity.Version);
        }

        public async Task<IReadOnlyCollection<Order>> GetByUserAsync(Guid userId, CancellationToken ct = default)
        {
            List<OrderEntity> entities = await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync(ct);

            return [.. entities.Select(MapToDomain)];
        }

        public async Task<bool> TryUpdateWithVersionAsync(Order? order, int expectedVersion, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(order);

            OrderEntity? entity = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == order.Id, ct);

            if (entity == null)
            {
                _logger.LogWarning("Order {OrderId} not found for update", order.Id);
                return false;
            }

            if (entity.Version != expectedVersion)
            {
                _logger.LogWarning("Concurrent update detected for order {OrderId}. Expected version: {ExpectedVersion}",
                    order.Id, expectedVersion);
                return false;
            }

            entity.Status = order.Status.ToString();
            entity.Amount = order.Amount.Amount;
            entity.Currency = order.Amount.Currency;
            entity.Description = order.Description.Value;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.Version = order.Version;

            try
            {
                _ = await _context.SaveChangesAsync(ct);
                _logger.LogDebug("Order updated. ID: {OrderId}, New version: {Version}", order.Id, entity.Version);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrent update conflict for order {OrderId}", order.Id);
                EntityEntry<OrderEntity> entry = _context.Entry(entity);
                entry.State = EntityState.Detached;

                return false;
            }
        }

        private OrderEntity MapToEntity(Order order)
        {
            return new OrderEntity
            {
                Id = order.Id,
                UserId = order.UserId,
                Amount = order.Amount.Amount,
                Currency = order.Amount.Currency,
                Description = order.Description.Value,
                Status = order.Status.ToString(),
                CreatedAt = order.CreatedAt,
                Version = order.Version
            };
        }

        private Order MapToDomain(OrderEntity entity)
        {
            Money amount = Money.Create(entity.Amount, entity.Currency);
            OrderDescription description = OrderDescription.Create(entity.Description);
            OrderStatus status = Enum.Parse<Domain.Enums.OrderStatus>(entity.Status);

            return Order.Recreate(
                id: entity.Id,
                userId: entity.UserId,
                amount: amount,
                description: description,
                status: status,
                createdAt: entity.CreatedAt,
                version: entity.Version);
        }
    }

    public class OrderEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "RUB"; // default currency
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "New";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public int Version { get; set; } = 1;

        public ICollection<OutboxEntity> OutboxMessages { get; set; } = [];
    }
}