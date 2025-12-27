using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderService.Application.Ports;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.Idempotency
{
    public class IdempotencyService(OrderDbContext context, ILogger<IdempotencyService> logger) : IIdempotencyService
    {
        private readonly OrderDbContext _context = context;
        private readonly ILogger<IdempotencyService> _logger = logger;

        public async Task<bool> WasProcessedAsync(string? idempotencyKey, CancellationToken ct = default)
        {
            return string.IsNullOrWhiteSpace(idempotencyKey)
                ? throw new ArgumentException("Idempotency key cannot be null or empty", nameof(idempotencyKey))
                : await _context.ProcessedMessages
                .AnyAsync(x => x.IdempotencyKey == idempotencyKey, ct);
        }

        public async Task MarkAsProcessedAsync(string? idempotencyKey, string details, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                throw new ArgumentException("Idempotency key cannot be null or empty", nameof(idempotencyKey));
            }

            ProcessedMessage processedMessage = new()
            {
                IdempotencyKey = idempotencyKey,
                Details = details,
                ProcessedAt = DateTimeOffset.UtcNow
            };

            _ = await _context.ProcessedMessages.AddAsync(processedMessage, ct);
            _ = await _context.SaveChangesAsync(ct);

            _logger.LogDebug("Marked message as processed. Key: {Key}", idempotencyKey);
        }
    }

    public class ProcessedMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string IdempotencyKey { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTimeOffset ProcessedAt { get; set; }

        public string? MessageId { get; set; }
        public string? MessageType { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
    }
}