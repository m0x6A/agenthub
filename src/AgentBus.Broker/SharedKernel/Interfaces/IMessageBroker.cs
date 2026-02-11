using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.SharedKernel.Interfaces;

public interface IMessageBroker
{
    Task<string> SendMessageAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default);
    Task<MessageEnvelope?> ReceiveMessageAsync(string agentId, int maxWaitTimeSeconds = 30, CancellationToken cancellationToken = default);
    Task AcknowledgeMessageAsync(string agentId, string messageId, CancellationToken cancellationToken = default);
    Task CreateInboxQueueAsync(string agentId, CancellationToken cancellationToken = default);
    Task DeleteInboxQueueAsync(string agentId, CancellationToken cancellationToken = default);
}
