using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderService.Application.Dtos;
using OrderService.Application.Ports;

namespace OrderService.Infrastructure.Persistence.Repositories
{
    public class OutboxRepository(OrderDbContext context, ILogger<OutboxRepository> logger) : IOutboxRepository
    {
        private readonly OrderDbContext _context = context;
        private readonly ILogger<OutboxRepository> _logger = logger;

        public async Task AddAsync(OutboxMessage? message, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            OutboxEntity entity = MapToEntity(message);
            _ = await _context.Outbox.AddAsync(entity, ct);
        }

        public async Task<IReadOnlyCollection<OutboxMessage>> GetUnprocessedBatchAsync(
            int batchSize, CancellationToken ct = default)
        {
            List<OutboxEntity> entities = await _context.Outbox
                .Where(x => !x.Processed)
                .OrderBy(x => x.CreatedAt)
                .Take(batchSize)
                .AsNoTracking()
                .ToListAsync(ct);

            return [.. entities.Select(MapToDto)];
        }

        public async Task MarkAsSentAsync(IEnumerable<Guid>? messageIds, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(messageIds);

            List<Guid> idsList = [.. messageIds];
            if (idsList.Count == 0)
            {
                return;
            }

            List<OutboxEntity> entities = await _context.Outbox
                .Where(x => idsList.Contains(x.Id))
                .ToListAsync(ct);

            foreach (OutboxEntity entity in entities)
            {
                entity.Processed = true;
                entity.ProcessedAt = DateTimeOffset.UtcNow;
            }

            _ = await _context.SaveChangesAsync(ct);
            _logger.LogDebug("Marked {Count} messages as sent", entities.Count);
        }

        private OutboxEntity MapToEntity(OutboxMessage dto)
        {
            return new OutboxEntity
            {
                Id = dto.Id,
                Type = dto.Type,
                Data = dto.Data,
                Queue = dto.Queue,
                CreatedAt = dto.CreatedAt,
                Processed = dto.Processed,
                ProcessedAt = dto.ProcessedAt,
                RetryCount = dto.RetryCount,
                Error = dto.Error
            };
        }

        private OutboxMessage MapToDto(OutboxEntity entity)
        {
            return new OutboxMessage(
                entity.Id,
                entity.Type,
                entity.Data,
                entity.Queue,
                entity.CreatedAt,
                entity.Processed,
                entity.ProcessedAt,
                entity.RetryCount,
                entity.Error);
        }
    }

    public class OutboxEntity
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public string? Queue { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool Processed { get; set; }
        public DateTimeOffset? ProcessedAt { get; set; }
        public int RetryCount { get; set; }
        public string? Error { get; set; }
        public Guid? OrderId { get; set; }
        public OrderEntity? Order { get; set; }
    }
}