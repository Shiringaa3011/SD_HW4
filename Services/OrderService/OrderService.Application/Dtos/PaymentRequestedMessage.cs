using System;

namespace OrderService.Application.Dtos
{
    public record PaymentRequestedMessage(
        Guid PaymentId,
        Guid OrderId,
        Guid UserId,
        decimal Amount,
        string Currency,
        DateTimeOffset RequestedAt);
}
