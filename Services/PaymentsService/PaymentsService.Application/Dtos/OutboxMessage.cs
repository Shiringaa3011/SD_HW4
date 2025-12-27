using System;
using System.Collections.Generic;
using System.Text;

namespace PaymentsService.Application.Dtos
{
    public class OutboxMessage(
        Guid messageId,
        string correlationId,
        string type,
        string body,
        string topic,
        DateTimeOffset createdAt,
        Guid? id = null
        )
    {
        public Guid Id { get; } = id ?? Guid.NewGuid();
        public Guid MessageId { get; } = messageId;
        public string CorrelationId { get; } = correlationId;
        public string Type { get; } = type ?? throw new ArgumentNullException(nameof(type));
        public string Body { get; } = body ?? throw new ArgumentNullException(nameof(body));
        public string Topic { get; } = topic ?? throw new ArgumentNullException(nameof(topic));
        public DateTimeOffset CreatedAt { get; } = createdAt;
        public DateTimeOffset? SentAt { get; private set; }

        public void MarkAsSent()
        {
            SentAt = DateTimeOffset.UtcNow;
        }
    }
}
