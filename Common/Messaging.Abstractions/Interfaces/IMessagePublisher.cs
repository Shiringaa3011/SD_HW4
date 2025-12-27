using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Messaging.Abstractions.Interfaces
{
    public interface IMessagePublisher
    {
        Task PublishAsync<T>(T message, string routingKey, string? exchange = null,
            Dictionary<string, object>? headers = null, CancellationToken ct = default) where T : class;
    }
}
