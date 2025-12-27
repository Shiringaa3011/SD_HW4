using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Common.Messaging.Abstractions.Interfaces;
using Common.Messaging.Abstractions.Dtos;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;

namespace Common.Messaging.RabbitMQ
{
    public class RabbitMqConsumer : IMessageConsumer, IDisposable
    {
        private readonly RabbitMqOptions _options;
        private readonly ILogger<RabbitMqConsumer> _logger;
        private IConnection? _connection;
        private IModel? _channel;
        private readonly string _consumerTag;
        private bool _disposed;

        public RabbitMqConsumer(
            IOptions<RabbitMqOptions> options,
            ILogger<RabbitMqConsumer> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _consumerTag = $"{Environment.MachineName}-{Guid.NewGuid():N}";

            InitializeConnection();
        }

        private void InitializeConnection()
        {
            try
            {
                ConnectionFactory factory = new()
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    UserName = _options.UserName,
                    Password = _options.Password,
                    VirtualHost = _options.VirtualHost,
                    AutomaticRecoveryEnabled = true,
                    DispatchConsumersAsync = true
                };

                _connection = factory.CreateConnection($"{_options.ConsumerGroup}-connection");
                _channel = _connection.CreateModel();

                _channel.BasicQos(
                    prefetchSize: 0,
                    prefetchCount: (ushort)_options.PrefetchCount,
                    global: false);

                _logger.LogInformation("RabbitMQ consumer initialized");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to initialize RabbitMQ connection");
                throw;
            }
        }

        public async Task SubscribeAsync(
            string queueName,
            Func<MessageEnvelope, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Subscribing to RabbitMQ queue: {QueueName}", queueName);

            try
            {
                _channel.ExchangeDeclare(
                    exchange: "orders-exchange",
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);

                _logger.LogInformation("Exchange 'orders-exchange' declared");

                if (_channel is null)
                {
                    throw new InvalidOperationException(
                        "RabbitMQ channel is not initialized. Call InitializeConnection() first.");
                }

                _ = _channel.QueueDeclare(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                _channel.QueueBind(
                    queue: queueName,
                    exchange: "orders-exchange",
                    routingKey: queueName);

                _logger.LogInformation("Queue {QueueName} declared and bound", queueName);

                AsyncEventingBasicConsumer consumer = new(_channel);
                consumer.Received += async (model, ea) => await OnMessageReceived(ea, handler, cancellationToken);

                _ = _channel.BasicConsume(
                    queue: queueName,
                    autoAck: false,
                    consumerTag: _consumerTag,
                    consumer: consumer);

                _logger.LogInformation("Successfully subscribed to queue: {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to queue: {QueueName}", queueName);
                throw;
            }
        }

        private async Task OnMessageReceived(
            BasicDeliverEventArgs ea,
            Func<MessageEnvelope, CancellationToken, Task> handler,
            CancellationToken cancellationToken)
        {
            string messageId = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString();

            _logger.LogDebug("Received raw message {MessageId} from queue {Queue}",
                messageId, ea.RoutingKey);

            try
            {
                string body = Encoding.UTF8.GetString(ea.Body.ToArray());

                string messageType = "Unknown";
                if (ea.BasicProperties.Headers != null &&
                    ea.BasicProperties.Headers.TryGetValue("x-message-type", out object? typeHeader))
                {
                    messageType = typeHeader is byte[] bytes
                        ? Encoding.UTF8.GetString(bytes)
                        : typeHeader?.ToString() ?? "Unknown";
                }

                MessageEnvelope message = new(
                    MessageId: messageId,
                    Body: body,
                    MessageType: messageType,
                    Headers: [],
                    Timestamp: DateTimeOffset.UtcNow,
                    RawMessage: ea);

                await handler(message, cancellationToken);

                _logger.LogDebug("Handler completed for message {MessageId}", messageId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Message processing cancelled for message {MessageId}", messageId);
                SafeNack(ea.DeliveryTag, requeue: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnMessageReceived for message {MessageId}", messageId);
                SafeNack(ea.DeliveryTag, requeue: true);
            }
        }

        public Task AcknowledgeAsync(MessageEnvelope message)
        {
            if (message.RawMessage is BasicDeliverEventArgs ea)
            {
                SafeAck(ea.DeliveryTag, message.MessageId);
                _logger.LogDebug("Manually acknowledged message {MessageId}", message.MessageId);
            }

            return Task.CompletedTask;
        }

        public Task RejectAsync(MessageEnvelope message, bool requeue = true)
        {
            if (message.RawMessage is BasicDeliverEventArgs ea)
            {
                SafeNack(ea.DeliveryTag, requeue: requeue);
                _logger.LogDebug("Message {MessageId} rejected. Requeue: {Requeue}",
                    message.MessageId, requeue);
            }

            return Task.CompletedTask;
        }

        private void SafeAck(ulong deliveryTag, string messageId)
        {
            try
            {
                if (_channel is null)
                {
                    _logger.LogError("Cannot ack message {MessageId}: channel is null", messageId);
                    return;
                }

                if (!_channel.IsOpen)
                {
                    _logger.LogError("Cannot ack message {MessageId}: channel is closed", messageId);
                    return;
                }

                _channel.BasicAck(deliveryTag, multiple: false);
                _logger.LogDebug("Ack sent for message {MessageId}", messageId);
            }
            catch (AlreadyClosedException ex)
            {
                _logger.LogError(ex, "Failed to ack message {MessageId}: channel was closed", messageId);
                _channel = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ack message {MessageId}", messageId);
            }
        }

        private void SafeNack(ulong deliveryTag, bool requeue)
        {
            try
            {
                if (_channel is null)
                {
                    _logger.LogError("Cannot Nack message {DeliveryTag}: channel is null", deliveryTag);
                    return;
                }

                if (!_channel.IsOpen)
                {
                    _logger.LogError("Cannot Nack message {DeliveryTag}: channel is closed", deliveryTag);
                    return;
                }

                _channel.BasicNack(deliveryTag, multiple: false, requeue: requeue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to Nack message {DeliveryTag}", deliveryTag);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _channel?.Close();
                _channel?.Dispose();
                _connection?.Close();
                _connection?.Dispose();
            }
        }
    }

    public class RabbitMqOptions
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string VirtualHost { get; set; } = "/";
        public string ConsumerGroup { get; set; } = "payments-service";
        public int PrefetchCount { get; set; } = 10;
        public int MaxRetryCount { get; set; } = 3;
        public string DefaultExchange { get; set; } = "orders-exchange";
    }
}
