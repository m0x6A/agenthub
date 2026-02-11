using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.SharedKernel.Interfaces;

public interface IEventBroker
{
    Task<string> PublishEventAsync(EventEnvelope envelope, CancellationToken cancellationToken = default);
    Task<Subscription> SubscribeAsync(string agentId, string eventType, string[]? filters = null, CancellationToken cancellationToken = default);
    Task<EventEnvelope?> ReceiveEventAsync(string subscriptionId, int maxWaitTimeSeconds = 30, CancellationToken cancellationToken = default);
    Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task DeleteAllSubscriptionsAsync(string agentId, CancellationToken cancellationToken = default);
}
