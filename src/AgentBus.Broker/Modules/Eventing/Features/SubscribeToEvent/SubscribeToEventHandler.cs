using AgentBus.Broker.SharedKernel.Interfaces;

namespace AgentBus.Broker.Modules.Eventing.Features.SubscribeToEvent;

public sealed class SubscribeToEventHandler(
    IEventBroker eventBroker,
    ILogger<SubscribeToEventHandler> logger)
{
    public async Task<SubscribeToEventResponse> HandleAsync(
        SubscribeToEventRequest request,
        string agentId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Agent {AgentId} subscribing to event type {EventType}", agentId, request.EventType);

        var subscription = await eventBroker.SubscribeAsync(
            agentId,
            request.EventType,
            request.Filters,
            cancellationToken);
        
        return new SubscribeToEventResponse(subscription);
    }
}
