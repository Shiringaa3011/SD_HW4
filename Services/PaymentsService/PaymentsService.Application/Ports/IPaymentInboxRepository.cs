namespace PaymentsService.Application.Ports
{
    public interface IPaymentInboxRepository
    {
        Task AddAsync(InboxMessage message, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default);
        Task<bool> TryAcquireAsync(string messageId, Guid orderId, Guid userId, CancellationToken ct);

        Task<bool> TryAcquireAsync(
            string messageId,
            Guid orderId,
            Guid userId,
            string processorId,
            CancellationToken cancellationToken = default);

        Task ReleaseAsync(string messageId, CancellationToken ct);

        Task MarkAsProcessedAsync(string messageId, CancellationToken ct);

        Task<List<InboxMessage>> GetPendingMessagesAsync(
            int batchSize = 50,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<InboxMessage>> GetStuckMessagesAsync(TimeSpan olderThan, CancellationToken ct);

        Task IncrementRetryCountAsync(string messageId, CancellationToken cancellationToken = default);

        Task MarkAsFailedAsync(string messageId, string error, CancellationToken cancellationToken = default);

    }

    public record InboxMessage(
        string Id,
        Guid OrderId,
        Guid UserId,
        string Body,
        string MessageType,
        InboxMessageStatus Status,
        int RetryCount,
        string? ProcessorId,
        DateTimeOffset? LockedAt,
        DateTimeOffset ReceivedAt,
        DateTimeOffset? ProcessedAt
    );

    public enum InboxMessageStatus
    {
        Pending,
        Processing,
        Processed,
        Failed,
        DeadLetter
    }
}