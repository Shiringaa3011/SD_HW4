using OrderService.Domain.Entities;

namespace OrderService.Application.Ports
{
    public interface IOrderRepository
    {
        Task AddAsync(Order order, CancellationToken ct = default);

        Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default);

        Task<(Order?, int)> GetByIdWithVersionAsync(Guid orderId, CancellationToken ct);

        Task<IReadOnlyCollection<Order>> GetByUserAsync(Guid userId, CancellationToken ct = default);

        Task<bool> TryUpdateWithVersionAsync(Order order, int expectedVersion, CancellationToken ct = default);
    }

}
