using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.SharedKernel.Interfaces;

public interface IAgentRegistry
{
    Task<Agent?> GetAgentByIdAsync(string agentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Agent>> FindAgentsByCapabilityAsync(string capability, CancellationToken cancellationToken = default);
    Task<IEnumerable<Agent>> GetAllAgentsAsync(AgentStatus? status = null, CancellationToken cancellationToken = default);
    Task<Agent> RegisterAgentAsync(Agent agent, CancellationToken cancellationToken = default);
    Task UpdateHeartbeatAsync(string agentId, DateTime heartbeatTime, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(string agentId, AgentStatus status, CancellationToken cancellationToken = default);
    Task DeleteAgentAsync(string agentId, CancellationToken cancellationToken = default);
    Task AddEventSubscriptionAsync(string agentId, EventSubscription subscription, CancellationToken cancellationToken = default);
    Task RemoveEventSubscriptionAsync(string agentId, string subscriptionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EventSubscription>> GetEventSubscriptionsAsync(string agentId, CancellationToken cancellationToken = default);
}
