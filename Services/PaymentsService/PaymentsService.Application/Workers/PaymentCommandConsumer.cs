using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentsService.Application.Dtos;
using PaymentsService.Application.Ports;
using System.Diagnostics;
using System.Text.Json;
using Common.Messaging.Abstractions.Interfaces;
using Common.Messaging.Abstractions.Dtos;

namespace PaymentsService.Application.Workers
{
    public class PaymentCommandConsumer(
        IMessageConsumer messageConsumer,
        IServiceProvider serviceProvider,
        ILogger<PaymentCommandConsumer> logger,
        IOptions<PaymentCommandConsumerOptions> options) : BackgroundService
    {
        private readonly IMessageConsumer _messageConsumer = messageConsumer ?? throw new ArgumentNullException(nameof(messageConsumer));
        private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        private readonly ILogger<PaymentCommandConsumer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly PaymentCommandConsumerOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(3000, stoppingToken);
            _logger.LogInformation("PaymentCommandConsumer starting...");

            await _messageConsumer.SubscribeAsync(
                queueName: _options.QueueName,
                handler: HandleMessageAsync,
                cancellationToken: stoppingToken);

            _logger.LogInformation("PaymentCommandConsumer subscribed to topic: {Topic}", _options.QueueName);
        }

        private async Task HandleMessageAsync(MessageEnvelope message, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Обработка сообщения начата: {MessageId}", message.MessageId);
            _logger.LogInformation("Тело сообщения: {Body}", message.Body);

            using Activity? activity = Diagnostics.ActivitySource.StartActivity("ProcessPaymentCommand");
            _ = (activity?.SetTag("message.id", message.MessageId));
            _ = (activity?.SetTag("message.type", message.MessageType));

            _logger.LogDebug("Received message {MessageId} of type {MessageType}",
                message.MessageId, message.MessageType);

            try
            {
                PaymentCommandDto? command = JsonSerializer.Deserialize<PaymentCommandDto>(message.Body) ?? throw new JsonException($"Failed to deserialize PaymentCommand from message {message.MessageId}");

                if (string.IsNullOrEmpty(command.MessageId))
                {
                    command = new PaymentCommandDto(message.MessageId, command.OrderId, command.UserId, command.Amount, command.Currency);
                }

                using IServiceScope scope = _serviceProvider.CreateScope();

                IPaymentInboxRepository inboxRepository = scope.ServiceProvider.GetRequiredService<IPaymentInboxRepository>();
                IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                await unitOfWork.BeginTransactionAsync(ct: cancellationToken);

                try
                {
                    bool exists = await inboxRepository.ExistsAsync(message.MessageId, cancellationToken);
                    if (exists)
                    {
                        _logger.LogInformation("Message {MessageId} already exists in inbox. Skipping...",
                            message.MessageId);
                    }
                    else
                    {
                        InboxMessage inboxMessage = new(
                            Id: message.MessageId,
                            OrderId: command.OrderId,
                            UserId: command.UserId,
                            Body: message.Body,
                            MessageType: message.MessageType,
                            Status: InboxMessageStatus.Pending,
                            RetryCount: 0,
                            ProcessorId: null,
                            LockedAt: null,
                            ReceivedAt: DateTimeOffset.UtcNow,
                            ProcessedAt: null);

                        await inboxRepository.AddAsync(inboxMessage, cancellationToken);

                        _logger.LogDebug("Message {MessageId} saved to inbox for order {OrderId}",
                            message.MessageId, command.OrderId);
                    }
                    await unitOfWork.CommitTransactionAsync(cancellationToken);

                    await _messageConsumer.AcknowledgeAsync(message);

                    _logger.LogInformation("Successfully processed incoming message {MessageId} for order {OrderId}",
                        message.MessageId, command.OrderId);

                    _ = (activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Ok));
                }
                catch (Exception ex)
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);

                    _logger.LogError(ex, "Failed to save message {MessageId} to inbox", message.MessageId);

                    await _messageConsumer.RejectAsync(message, requeue: true);

                    _ = (activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error));
                    _ = (activity?.SetTag("error.message", ex.Message));

                    throw;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid message format for {MessageId}", message.MessageId);

                await _messageConsumer.RejectAsync(message, requeue: false);

                _ = (activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error));
                _ = (activity?.SetTag("error.type", "InvalidFormat"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing message {MessageId}", message.MessageId);

                await _messageConsumer.RejectAsync(message, requeue: true);

                _ = (activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error));
                _ = (activity?.SetTag("error.type", "Unexpected"));

                throw;
            }
        }
    }
    public class PaymentCommandConsumerOptions
    {
        public string QueueName { get; set; } = "payment-requests";
        public string ConsumerGroup { get; set; } = "payment-requests";
        public int BatchSize { get; set; } = 100;
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    }

    public static class Diagnostics
    {
        public static readonly System.Diagnostics.ActivitySource ActivitySource =
            new("PaymentsService.MessageConsuming");
    }

}
