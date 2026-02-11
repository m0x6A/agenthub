namespace AgentBus.Examples.Shared;

public interface IAgentBusClient : IDisposable
{
    Task RegisterAgentAsync(AgentRegistration registration);
    Task<Subscription> SubscribeToAllEventsAsync();
    Task<Subscription> SubscribeToEventAsync(string eventType);
    Task PublishEventAsync(string eventType, object data, string? correlationId = null);
    Task<EventEnvelope?> ReceiveEventAsync(string subscriptionId, int maxWaitSeconds, CancellationToken cancellationToken);
}
