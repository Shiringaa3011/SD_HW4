using PaymentsService.Application.Dtos;

namespace PaymentsService.Application.Ports
{
    public interface IPaymentOutboxRepository
    {
        Task AddAsync(OutboxMessage message, CancellationToken ct = default);
        Task MarkAsPublishedAsync(Guid messageId, CancellationToken ct = default);
        Task<OutboxMessage?> FindByMessageIdAsync(string messageId, CancellationToken ct = default);
        Task<List<OutboxMessage>> GetPendingMessagesAsync(int batchSize, CancellationToken ct);
        Task<bool> TryAcquireForSendingAsync(Guid messageId, CancellationToken ct);

        Task ReleaseAsync(Guid messageId, CancellationToken ct = default);
    }

}
