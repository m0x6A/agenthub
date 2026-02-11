using AgentBus.Broker.SharedKernel.Interfaces;

namespace AgentBus.Broker.Modules.Registration.Features.DeregisterAgent;

public sealed class DeregisterAgentHandler
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IMessageBroker _messageBroker;
    private readonly IEventBroker _eventBroker;

    public DeregisterAgentHandler(
        IAgentRegistry agentRegistry,
        IMessageBroker messageBroker,
        IEventBroker eventBroker)
    {
        _agentRegistry = agentRegistry;
        _messageBroker = messageBroker;
        _eventBroker = eventBroker;
    }

    public async Task HandleAsync(DeregisterAgentRequest request, CancellationToken cancellationToken)
    {
        var agent = await _agentRegistry.GetAgentByIdAsync(request.AgentId, cancellationToken);
        
        if (agent is null)
        {
            throw new InvalidOperationException($"Agent with ID '{request.AgentId}' not found");
        }

        // Delete subscriptions first
        await _eventBroker.DeleteAllSubscriptionsAsync(request.AgentId, cancellationToken);

        // Delete inbox queue (using agent ID per interface)
        await _messageBroker.DeleteInboxQueueAsync(request.AgentId, cancellationToken);

        // Delete agent from registry
        await _agentRegistry.DeleteAgentAsync(request.AgentId, cancellationToken);
    }
}
