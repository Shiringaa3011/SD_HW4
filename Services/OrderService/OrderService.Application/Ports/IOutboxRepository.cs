using OrderService.Application.Dtos;

namespace OrderService.Application.Ports
{
    public interface IOutboxRepository
    {
        Task AddAsync(OutboxMessage message, CancellationToken ct = default);

        Task<IReadOnlyCollection<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize, CancellationToken ct = default);

        Task MarkAsSentAsync(IEnumerable<Guid> messageIds, CancellationToken ct = default);
    }

}
