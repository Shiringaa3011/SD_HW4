using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Common.Messaging.Abstractions.Interfaces;

namespace Common.Messaging.RabbitMQ
{
    public class RabbitMqPublisher : IMessagePublisher, IDisposable
    {
        private readonly RabbitMqOptions _options;
        private readonly ILogger<RabbitMqPublisher> _logger;
        private IConnection? _connection;
        private IModel? _channel;
        private readonly object _lock = new();
        private bool _disposed;

        public RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger)
        {
            _options = options.Value;
            _logger = logger;

            _logger.LogInformation("RabbitMqPublisher created with options: HostName={HostName}, Exchange={Exchange}",
                _options.HostName, _options.DefaultExchange);
        }

        public async Task PublishAsync<T>(T message, string routingKey, string? exchange = null,
            Dictionary<string, object>? headers = null, CancellationToken ct = default) where T : class
        {
            EnsureConnection();

            if (_channel == null || !_channel.IsOpen)
            {
                throw new InvalidOperationException("RabbitMQ channel is not available");
            }

            string messageId = Guid.NewGuid().ToString();
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            try
            {
                IBasicProperties properties = _channel.CreateBasicProperties();
                properties.MessageId = messageId;
                properties.Timestamp = new AmqpTimestamp(timestamp);
                properties.Persistent = true;
                properties.ContentType = "application/json";
                properties.Headers = headers ?? [];

                properties.Headers["x-message-id"] = messageId;
                properties.Headers["x-message-type"] = "PaymentRequested";
                properties.Headers["x-timestamp"] = timestamp;

                byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
                string targetExchange = exchange ?? _options.DefaultExchange;

                _channel.BasicPublish(
                    exchange: targetExchange,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body);

                _logger.LogDebug("Message published. ID: {MessageId}, Exchange: {Exchange}, RoutingKey: {RoutingKey}",
                    messageId, targetExchange, routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message. RoutingKey: {RoutingKey}", routingKey);
                throw;
            }
        }

        private void EnsureConnection()
        {
            if (_connection != null && _connection.IsOpen &&
                _channel != null && _channel.IsOpen)
            {
                _logger.LogDebug("RabbitMQ connection already established");
                return;
            }

            lock (_lock)
            {
                if (_connection != null && _connection.IsOpen &&
                    _channel != null && _channel.IsOpen)
                {
                    _logger.LogDebug("RabbitMQ connection already established (in lock)");
                    return;
                }

                _logger.LogInformation("Establishing RabbitMQ connection...");

                ConnectionFactory factory = new()
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    UserName = _options.UserName,
                    Password = _options.Password,
                    VirtualHost = _options.VirtualHost,
                    DispatchConsumersAsync = true,
                    AutomaticRecoveryEnabled = true
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _logger.LogInformation("RabbitMQ connection established. Creating exchanges and queues...");

                _channel.ExchangeDeclare(
                    exchange: _options.DefaultExchange,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);

                _ = _channel.QueueDeclare(
                    queue: "payment-requests",
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                _channel.QueueBind(
                    queue: "payment-requests",
                    exchange: _options.DefaultExchange,
                    routingKey: "payment-requests");

                _ = _channel.QueueDeclare(
                    queue: "payment-results",
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                _channel.QueueBind(
                    queue: "payment-results",
                    exchange: _options.DefaultExchange,
                    routingKey: "payment-results");

                _logger.LogInformation("RabbitMQ exchanges and queues created successfully");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
        }
    }

}
