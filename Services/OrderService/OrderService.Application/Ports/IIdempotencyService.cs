using System;

namespace OrderService.Application.Ports
{
    public interface IIdempotencyService
    {
        Task<bool> WasProcessedAsync(string idempotencyKey, CancellationToken ct = default);

        Task MarkAsProcessedAsync(string idempotencyKey, string details, CancellationToken ct = default);
    }
}
