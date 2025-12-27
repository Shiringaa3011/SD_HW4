using OrderService.Application.Dtos;
using OrderService.Application.Ports;
using OrderService.Domain.Entities;

namespace OrderService.Application.UseCases
{
    public class ListOrdersUseCase(IOrderRepository orders)
    {
        private readonly IOrderRepository _orders = orders;

        public async Task<IReadOnlyCollection<OrderDto>> HandleAsync(Guid userId, CancellationToken ct = default)
        {
            IReadOnlyCollection<Order> orders = await _orders.GetByUserAsync(userId, ct);

            return [.. orders
                .Select(o => new OrderDto(
                    o.Id,
                    o.UserId,
                    o.Amount.Amount,
                    o.Amount.Currency,
                    o.Description.Value,
                    o.Status.ToString(),
                    o.CreatedAt))];
        }
    }
}
