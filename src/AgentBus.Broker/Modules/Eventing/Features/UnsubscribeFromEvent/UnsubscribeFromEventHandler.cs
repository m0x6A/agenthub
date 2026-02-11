using AgentBus.Broker.SharedKernel.Interfaces;

namespace AgentBus.Broker.Modules.Eventing.Features.UnsubscribeFromEvent;

public sealed class UnsubscribeFromEventHandler(
    IEventBroker eventBroker,
    ILogger<UnsubscribeFromEventHandler> logger)
{
    public async Task HandleAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Unsubscribing from subscription {SubscriptionId}", subscriptionId);
        await eventBroker.UnsubscribeAsync(subscriptionId, cancellationToken);
    }
}
