using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.SharedKernel.Interfaces;

public interface IEventBroker
{
    /// <summary>
    /// Publishes an event to both specific event type topic and global broadcast topic
    /// </summary>
    Task<string> PublishEventAsync(EventEnvelope envelope, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Subscribes an agent to a specific event type
    /// </summary>
    Task<Subscription> SubscribeAsync(string agentId, string eventType, string[]? filters = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Subscribes an agent to the global broadcast topic to receive ALL events
    /// </summary>
    Task<Subscription> SubscribeToAllEventsAsync(string agentId, CancellationToken cancellationToken = default);
    
    Task<EventEnvelope?> ReceiveEventAsync(string subscriptionId, int maxWaitTimeSeconds = 30, CancellationToken cancellationToken = default);
    Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task DeleteAllSubscriptionsAsync(string agentId, CancellationToken cancellationToken = default);
}
