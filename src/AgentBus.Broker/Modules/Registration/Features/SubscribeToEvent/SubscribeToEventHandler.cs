using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.Modules.Registration.Features.SubscribeToEvent;

public sealed class SubscribeToEventHandler
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IEventBroker _eventBroker;
    private readonly ILogger<SubscribeToEventHandler> _logger;

    public SubscribeToEventHandler(
        IAgentRegistry agentRegistry,
        IEventBroker eventBroker,
        ILogger<SubscribeToEventHandler> logger)
    {
        _agentRegistry = agentRegistry;
        _eventBroker = eventBroker;
        _logger = logger;
    }

    public async Task<EventSubscription> HandleAsync(
        string agentId,
        SubscribeToEventRequest request,
        CancellationToken cancellationToken)
    {
        // Verify agent exists
        var agent = await _agentRegistry.GetAgentByIdAsync(agentId, cancellationToken);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent {agentId} not found");
        }

        // Create Service Bus subscription
        var serviceBusSubscription = await _eventBroker.SubscribeAsync(
            agentId,
            request.EventType,
            request.Filters?.Values.ToArray(),
            cancellationToken);

        // Create event subscription record
        var eventSubscription = new EventSubscription(
            EventType: request.EventType,
            SubscriptionId: serviceBusSubscription.Id,
            Filters: request.Filters);

        // Add to agent registry
        await _agentRegistry.AddEventSubscriptionAsync(agentId, eventSubscription, cancellationToken);

        _logger.LogInformation(
            "Agent {AgentId} subscribed to event type {EventType} with subscription {SubscriptionId}",
            agentId, request.EventType, eventSubscription.SubscriptionId);

        return eventSubscription;
    }
}
