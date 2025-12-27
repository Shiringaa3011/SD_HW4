using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentsService.Application.Dtos;
using PaymentsService.Application.Ports;
using PaymentsService.Application.UseCases;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace PaymentsService.Application.Workers
{
    public class InboxProcessor(
        IServiceProvider serviceProvider,
        ILogger<InboxProcessor> logger,
        IOptions<InboxProcessorOptions> options) : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        private readonly ILogger<InboxProcessor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly InboxProcessorOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        private readonly string _processorId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("InboxProcessor {ProcessorId} starting...", _processorId);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                    await Task.Delay(_options.PollInterval, stoppingToken);
                }
                catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in InboxProcessor main loop");

                    await Task.Delay(_options.ErrorRetryDelay, stoppingToken);
                }
            }

            _logger.LogInformation("InboxProcessor {ProcessorId} stopping...", _processorId);
        }

        private async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            using Activity? activity = Diagnostics.ActivitySource.StartActivity("ProcessInboxBatch");

            using IServiceScope scope = _serviceProvider.CreateScope();
            IPaymentInboxRepository inboxRepository = scope.ServiceProvider.GetRequiredService<IPaymentInboxRepository>();

            List<InboxMessage> messages = await inboxRepository.GetPendingMessagesAsync(
                batchSize: _options.BatchSize,
                cancellationToken: cancellationToken);

            if (messages.Count == 0)
            {
                _ = (activity?.SetTag("batch.size", 0));
                return;
            }

            _ = (activity?.SetTag("batch.size", messages.Count));
            _logger.LogDebug("Processing batch of {Count} messages", messages.Count);

            int processedCount = 0;
            int failedCount = 0;

            foreach (InboxMessage message in messages)
            {
                try
                {
                    bool success = await ProcessSingleMessageAsync(message, cancellationToken);

                    if (success)
                    {
                        processedCount++;
                    }
                    else
                    {
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogError(ex, "Failed to process inbox message {MessageId}", message.Id);

                    await inboxRepository.IncrementRetryCountAsync(message.Id, cancellationToken);

                    if (message.RetryCount >= _options.MaxRetryCount)
                    {
                        await inboxRepository.MarkAsFailedAsync(
                            message.Id,
                            $"Max retry count ({_options.MaxRetryCount}) exceeded. Error: {ex.Message}",
                            cancellationToken);
                    }
                }
            }

            _ = (activity?.SetTag("processed.count", processedCount));
            _ = (activity?.SetTag("failed.count", failedCount));

            _logger.LogInformation("Batch processed: {Processed} success, {Failed} failed",
                processedCount, failedCount);
        }

        private async Task<bool> ProcessSingleMessageAsync(
            InboxMessage inboxMessage,
            CancellationToken cancellationToken)
        {
            using Activity? activity = Diagnostics.ActivitySource.StartActivity("ProcessInboxMessage");
            _ = (activity?.SetTag("message.id", inboxMessage.Id));
            _ = (activity?.SetTag("order.id", inboxMessage.OrderId));

            _logger.LogDebug("Processing inbox message {MessageId} for order {OrderId}",
                inboxMessage.Id, inboxMessage.OrderId);

            using IServiceScope scope = _serviceProvider.CreateScope();
            IPaymentInboxRepository inboxRepository = scope.ServiceProvider.GetRequiredService<IPaymentInboxRepository>();

            bool acquired = await inboxRepository.TryAcquireAsync(
                messageId: inboxMessage.Id,
                orderId: inboxMessage.OrderId,
                userId: inboxMessage.UserId,
                processorId: _processorId,
                cancellationToken: cancellationToken);

            if (!acquired)
            {
                _logger.LogDebug("Message {MessageId} already being processed by another instance", inboxMessage.Id);
                return false; // Сообщение уже обрабатывается
            }

            try
            {
                PaymentCommandDto? command = JsonSerializer.Deserialize<PaymentCommandDto>(inboxMessage.Body) ?? throw new JsonException($"Failed to deserialize command from inbox message {inboxMessage.Id}");

                command = new PaymentCommandDto(inboxMessage.Id, command.OrderId, command.UserId, command.Amount, command.Currency);

                ProcessPaymentUseCase useCase = scope.ServiceProvider.GetRequiredService<ProcessPaymentUseCase>();
                await useCase.HandleAsync(command, cancellationToken);

                await inboxRepository.MarkAsProcessedAsync(inboxMessage.Id, cancellationToken);

                _logger.LogInformation("Successfully processed inbox message {MessageId} for order {OrderId}",
                    inboxMessage.Id, inboxMessage.OrderId);

                _ = (activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Ok));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing inbox message {MessageId}", inboxMessage.Id);

                await inboxRepository.ReleaseAsync(inboxMessage.Id, cancellationToken);

                _ = (activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error));
                _ = (activity?.SetTag("error.message", ex.Message));

                throw;
            }
        }
    }

    public class InboxProcessorOptions
    {
        public int BatchSize { get; set; } = 50;
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan ErrorRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
        public int MaxRetryCount { get; set; } = 3;
        public bool EnableDeadLetterQueue { get; set; } = true;
    }

}
