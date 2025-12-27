using OrderService.Application.Dtos;
using OrderService.Application.Ports;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace OrderService.Application.UseCases
{
    public class ApplyPaymentStatusUseCase(
        IOrderRepository orders,
        IIdempotencyService idempotencyService,
        ILogger<ApplyPaymentStatusUseCase> logger)
    {
        private readonly IOrderRepository _orders = orders;
        private readonly IIdempotencyService _idempotencyService = idempotencyService;
        private readonly ILogger<ApplyPaymentStatusUseCase> _logger = logger;

        public async Task HandleAsync(PaymentStatusDto paymentStatus, CancellationToken ct = default)
        {
            string idempotencyKey = paymentStatus.MessageId ?? paymentStatus.PaymentId.ToString();
            if (await _idempotencyService.WasProcessedAsync(idempotencyKey, ct))
            {
                _logger.LogInformation("Payment status message already processed. PaymentId: {PaymentId}, OrderId: {OrderId}",
                    paymentStatus.PaymentId, paymentStatus.OrderId);
                return;
            }

            (Order? order, int loadedVersion) = await _orders.GetByIdWithVersionAsync(paymentStatus.OrderId, ct);
            if (order == null)
            {
                throw new OrderNotFoundException(paymentStatus.OrderId);
            }

            ValidateStatusTransition(order, paymentStatus);

            if (paymentStatus.Success)
            {
                order.MarkFinished();
            }
            else
            {
                order.MarkCancelled();
            }

            bool updated = await _orders.TryUpdateWithVersionAsync(order, loadedVersion, ct);
            if (!updated)
            {
                _logger.LogWarning("Concurrent update detected for order {OrderId}. Retrying...",
                    paymentStatus.OrderId);

                throw new Exception($"Order {paymentStatus.OrderId} was modified concurrently");
            }
            await _idempotencyService.MarkAsProcessedAsync(
                idempotencyKey,
                $"Order_{paymentStatus.OrderId}_Payment_{paymentStatus.PaymentId}",
                ct);

            _logger.LogInformation("Order {OrderId} status updated to {Status} after payment {PaymentId}",
                order.Id, order.Status, paymentStatus.PaymentId);
        }

        private void ValidateStatusTransition(Order order, PaymentStatusDto paymentStatus)
        {
            if (order.Status is OrderStatus.Finished or OrderStatus.Cancelled)
            {
                if ((paymentStatus.Success && order.Status == OrderStatus.Finished) ||
                    ((!paymentStatus.Success) && order.Status == OrderStatus.Cancelled))
                {
                    throw new Exception(
                        $"Order {order.Id} is already in final state {order.Status}");
                }

                throw new InvalidOperationException(
                    $"Cannot change status of order {order.Id} from {order.Status} to " +
                    $"{(paymentStatus.Success ? "Paid" : "Cancelled")}");
            }

            if (order.Status != OrderStatus.New)
            {
                throw new InvalidOperationException(
                    $"Cannot apply payment status to order {order.Id} in state {order.Status}. " +
                    $"Expected: {OrderStatus.New}");
            }
        }
    }
}
