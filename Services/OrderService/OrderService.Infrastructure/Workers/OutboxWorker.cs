using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderService.Application.Dtos;
using OrderService.Application.Ports;
using Common.Messaging.Abstractions.Interfaces;
using System.Text.Json;

namespace OrderService.Infrastructure.Workers
{
    public class OutboxWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxWorker> _logger;
        private readonly OutboxWorkerOptions _options;
        private readonly IMessagePublisher _publisher;

        public OutboxWorker(
            IServiceProvider serviceProvider,
            ILogger<OutboxWorker> logger,
            IOptions<OutboxWorkerOptions> options,
            IMessagePublisher publisher)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options.Value;
            _publisher = publisher;
            _logger.LogInformation("OutboxWorker CONSTRUCTOR called at {Time}", DateTime.UtcNow);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxWorker ExecuteAsync called at {Time}", DateTime.UtcNow);
            _logger.LogInformation("Outbox worker started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessagesAsync(stoppingToken);
                    await Task.Delay(_options.PollingInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in outbox worker");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }

            _logger.LogInformation("Outbox worker stopped");
        }

        private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            IOutboxRepository outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            IReadOnlyCollection<OutboxMessage> messages = await outboxRepository.GetUnprocessedBatchAsync(_options.BatchSize, ct);

            if (messages.Count == 0)
            {
                return;
            }

            _logger.LogDebug("Processing {Count} outbox messages", messages.Count);

            List<Guid> sentMessageIds = [];

            foreach (OutboxMessage message in messages)
            {
                try
                {
                    await ProcessSingleMessageAsync(message, ct);
                    sentMessageIds.Add(message.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                }
            }

            if (sentMessageIds.Count != 0)
            {
                await outboxRepository.MarkAsSentAsync(sentMessageIds, ct);
            }
        }

        private async Task ProcessSingleMessageAsync(OutboxMessage message, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(message.Queue))
            {
                _logger.LogWarning("Message {MessageId} has no queue specified", message.Id);
                return;
            }

            Type? messageType = message.Type switch
            {
                "PaymentRequested" => typeof(PaymentRequestedMessage),
                _ => null
            };

            if (messageType == null)
            {
                _logger.LogWarning("Unknown message type: {Type}", message.Type);
                return;
            }

            object? payload = JsonSerializer.Deserialize(message.Data, messageType);
            if (payload == null)
            {
                _logger.LogWarning("Failed to deserialize message {MessageId}", message.Id);
                return;
            }

            await _publisher.PublishAsync(
                payload,
                message.Queue,
                headers: new Dictionary<string, object>
                {
                    ["x-outbox-id"] = message.Id.ToString(),
                    ["x-original-type"] = message.Type
                },
                ct: ct);
        }
    }

    public class OutboxWorkerOptions
    {
        public int BatchSize { get; set; } = 100;
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
    }
}