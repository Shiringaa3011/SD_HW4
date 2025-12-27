using System;
using System.Collections.Generic;
using System.Text;

namespace OrderService.Application.Dtos
{
    public record PaymentResultDto(
        string MessageId,
        Guid OrderId,
        Guid UserId,
        bool Success,
        string? Reason = null);
}
