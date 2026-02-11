using AgentBus.Broker.SharedKernel.Interfaces;

namespace AgentBus.Broker.Modules.Registration.Features.UnsubscribeFromEvent;

public sealed class UnsubscribeFromEventHandler
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IEventBroker _eventBroker;
    private readonly ILogger<UnsubscribeFromEventHandler> _logger;

    public UnsubscribeFromEventHandler(
        IAgentRegistry agentRegistry,
        IEventBroker eventBroker,
        ILogger<UnsubscribeFromEventHandler> logger)
    {
        _agentRegistry = agentRegistry;
        _eventBroker = eventBroker;
        _logger = logger;
    }

    public async Task HandleAsync(
        string agentId,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        // Verify agent exists
        var agent = await _agentRegistry.GetAgentByIdAsync(agentId, cancellationToken);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent {agentId} not found");
        }

        // Remove from Service Bus
        await _eventBroker.UnsubscribeAsync(subscriptionId, cancellationToken);

        // Remove from agent registry
        await _agentRegistry.RemoveEventSubscriptionAsync(agentId, subscriptionId, cancellationToken);

        _logger.LogInformation(
            "Agent {AgentId} unsubscribed from subscription {SubscriptionId}",
            agentId, subscriptionId);
    }
}
