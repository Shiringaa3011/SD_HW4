using System;

namespace OrderService.Application.Dtos
{
    public record OutboxMessage(
        Guid Id,
        string Type,
        string Data,
        string? Queue,
        DateTimeOffset CreatedAt,
        bool Processed = false,
        DateTimeOffset? ProcessedAt = null,
        int RetryCount = 0,
        string? Error = null);
}
