using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Messaging.Abstractions.Dtos
{
    public record MessageEnvelope(
        string MessageId,
        string Body,
        string MessageType,
        Dictionary<string, string> Headers,
        DateTimeOffset Timestamp,
        object? RawMessage = null);
}
