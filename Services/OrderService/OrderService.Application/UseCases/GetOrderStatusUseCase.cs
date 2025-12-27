using OrderService.Application.Dtos;
using OrderService.Application.Ports;
using OrderService.Domain.Entities;
using OrderService.Domain.Exceptions;

namespace OrderService.Application.UseCases
{
    public class GetOrderStatusUseCase(IOrderRepository orders)
    {
        private readonly IOrderRepository _orders = orders;

        public async Task<OrderDto> HandleAsync(Guid orderId, CancellationToken ct = default)
        {
            Order order = await _orders.GetByIdAsync(orderId, ct) ?? throw new OrderNotFoundException(orderId);

            return new OrderDto(
                order.Id,
                order.UserId,
                order.Amount.Amount,
                order.Amount.Currency,
                order.Description.Value,
                order.Status.ToString(),
                order.CreatedAt);
        }
    }

}
