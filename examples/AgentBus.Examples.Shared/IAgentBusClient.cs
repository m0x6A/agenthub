namespace AgentBus.Examples.Shared;

public interface IAgentBusClient : IDisposable
{
    Task RegisterAgentAsync(AgentRegistration registration);
    Task<Subscription> SubscribeToAllEventsAsync();
    Task<Subscription> SubscribeToEventAsync(string eventType);
    Task PublishEventAsync(string eventType, object data, string? correlationId = null);
    Task<EventEnvelope?> ReceiveEventAsync(string subscriptionId, int maxWaitSeconds, CancellationToken cancellationToken);
    
    // Direct messaging between agents
    Task<AgentRegistration?> GetAgentAsync(string agentId);
    Task SendDirectMessageAsync(string recipientAgentId, string message);
    Task<DirectMessage?> ReceiveDirectMessageAsync(int maxWaitSeconds, CancellationToken cancellationToken);
}

public record DirectMessage(
    string MessageId,
    string From,
    string To,
    string Message,
    DateTime Timestamp);

