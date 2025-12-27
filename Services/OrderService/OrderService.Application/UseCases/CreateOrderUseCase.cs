using OrderService.Application.Dtos;
using OrderService.Application.Ports;
using OrderService.Domain.Entities;
using OrderService.Domain.ValueTypes;
using System.Text.Json;

namespace OrderService.Application.UseCases
{
    public class CreateOrderUseCase(IUnitOfWork unitOfWork, IOrderRepository orders, IOutboxRepository paymentCommands)
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IOrderRepository _orders = orders;
        private readonly IOutboxRepository _outbox = paymentCommands;

        public async Task<OrderDto> HandleAsync(CreateOrderDto request, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            Order order = Order.Create(request.UserId, Money.Create(request.Amount), OrderService.Domain.ValueTypes.OrderDescription.Create(request.Description), DateTimeOffset.Now);

            PaymentRequestedMessage messageContent = new(
                PaymentId: Guid.NewGuid(),
                OrderId: order.Id,
                UserId: order.UserId,
                Amount: order.Amount.Amount,
                Currency: order.Amount.Currency,
                RequestedAt: DateTimeOffset.UtcNow);

            OutboxMessage outboxMessage = new(Guid.NewGuid(), "PaymentRequested", JsonSerializer.Serialize(messageContent
            ), "payment-requests", DateTimeOffset.UtcNow);

            await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                await _orders.AddAsync(order, ct);
                await _outbox.AddAsync(outboxMessage, ct);
                await _unitOfWork.CommitTransactionAsync(ct);

                return new OrderDto(
                    order.Id,
                    order.UserId,
                    order.Amount.Amount,
                    order.Amount.Currency,
                    order.Description.Value,
                    order.Status.ToString(),
                    order.CreatedAt);
            }
            catch (Exception)
            {
                // При ошибке — откатываем ВСЁ
                await _unitOfWork.RollbackTransactionAsync(ct);
                throw;
            }
        }

    }

}
