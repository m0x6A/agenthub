using AgentBus.Broker.SharedKernel.Interfaces;

namespace AgentBus.Broker.Modules.Eventing.Features.ReceiveEvent;

public sealed class ReceiveEventHandler(
    IEventBroker eventBroker,
    ILogger<ReceiveEventHandler> logger)
{
    public async Task<ReceiveEventResponse> HandleAsync(
        string subscriptionId,
        int timeout,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Receiving event for subscription {SubscriptionId} with timeout {Timeout}s", subscriptionId, timeout);
        
        var eventEnvelope = await eventBroker.ReceiveEventAsync(subscriptionId, timeout, cancellationToken);
        return new ReceiveEventResponse(eventEnvelope);
    }
}
