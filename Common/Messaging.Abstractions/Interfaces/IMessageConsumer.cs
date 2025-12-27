using Common.Messaging.Abstractions.Dtos;

namespace Common.Messaging.Abstractions.Interfaces
{
    public interface IMessageConsumer
    {
        Task SubscribeAsync(string queueName,
            Func<MessageEnvelope, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default);

        Task AcknowledgeAsync(MessageEnvelope message);

        Task RejectAsync(MessageEnvelope message, bool requeue = true);
    }    
}