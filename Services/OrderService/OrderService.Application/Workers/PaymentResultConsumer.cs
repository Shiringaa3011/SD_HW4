using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Application.Ports;
using OrderService.Application.Dtos;
using System.Text.Json;
using Common.Messaging.Abstractions.Interfaces;
using Common.Messaging.Abstractions.Dtos;

namespace OrderService.Application.Workers
{
    public class PaymentResultConsumer(
        IMessageConsumer messageConsumer,
        IServiceProvider serviceProvider,
        ILogger<PaymentResultConsumer> logger) : BackgroundService
    {
        private readonly IMessageConsumer _messageConsumer = messageConsumer;
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ILogger<PaymentResultConsumer> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PaymentResultConsumer запущен");

            await Task.Delay(3000, stoppingToken);

            try
            {
                await _messageConsumer.SubscribeAsync(
                    queueName: "payment-results",
                    handler: HandlePaymentResultAsync,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("Подписан на очередь: payment-results");

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка подписки на очередь payment-results");
                throw;
            }
        }

        private async Task HandlePaymentResultAsync(MessageEnvelope message, CancellationToken ct)
        {
            _logger.LogInformation("Получен результат платежа: {MessageId}", message.MessageId);

            try
            {
                PaymentResultDto? result = JsonSerializer.Deserialize<PaymentResultDto>(message.Body) ?? throw new JsonException("Не удалось десериализовать PaymentResult");
                _logger.LogInformation("Результат для заказа {OrderId}: Success={Success}",
                    result.OrderId, result.Success);

                bool updated = await UpdateOrderStatusWithVersionAsync(result, ct);

                if (updated)
                {
                    _logger.LogInformation("Статус заказа {OrderId} обновлен", result.OrderId);
                    await _messageConsumer.AcknowledgeAsync(message);
                }
                else
                {
                    _logger.LogWarning("Не удалось обновить заказ {OrderId} (конкурентное изменение?)",
                        result.OrderId);
                    await _messageConsumer.RejectAsync(message, requeue: true);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Неверный формат JSON");
                await _messageConsumer.RejectAsync(message, requeue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки результата платежа");
                await _messageConsumer.RejectAsync(message, requeue: true);
            }
        }

        private async Task<bool> UpdateOrderStatusWithVersionAsync(PaymentResultDto result, CancellationToken ct)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            IOrderRepository orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

            (Domain.Entities.Order? order, int version) = await orderRepository.GetByIdWithVersionAsync(result.OrderId, ct);

            if (order == null)
            {
                _logger.LogWarning("Заказ {OrderId} не найден", result.OrderId);
                return false;
            }

            if (order.UserId != result.UserId)
            {
                _logger.LogWarning("Заказ {OrderId} не принадлежит пользователю {UserId}",
                    result.OrderId, result.UserId);
                return false;
            }

            if (order.Status != Domain.Enums.OrderStatus.New)
            {
                _logger.LogInformation("Заказ {OrderId} уже имеет статус {Status}. Пропускаем.",
                    result.OrderId, order.Status);
                return true;
            }

            if (result.Success)
            {
                order.MarkFinished();
            }
            else
            {
                order.MarkCancelled();
            }

            return await orderRepository.TryUpdateWithVersionAsync(order, version, ct);
        }
    }    
}