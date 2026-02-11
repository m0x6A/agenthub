using Microsoft.AspNetCore.Mvc;

namespace AgentBus.Broker.Modules.Eventing.Features.UnsubscribeFromEvent;

public static class UnsubscribeFromEventEndpoint
{
    public static async Task<IResult> Handle(
        [FromRoute] string subscriptionId,
        [FromServices] UnsubscribeFromEventHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(subscriptionId, cancellationToken);
        return Results.NoContent();
    }
}
