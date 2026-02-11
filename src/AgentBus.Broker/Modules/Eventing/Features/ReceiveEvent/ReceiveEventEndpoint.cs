using Microsoft.AspNetCore.Mvc;

namespace AgentBus.Broker.Modules.Eventing.Features.ReceiveEvent;

public static class ReceiveEventEndpoint
{
    public static async Task<IResult> Handle(
        [FromRoute] string subscriptionId,
        [FromServices] ReceiveEventHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] int timeout = 30)
    {
        var response = await handler.HandleAsync(subscriptionId, timeout, cancellationToken);
        return response.Event != null ? Results.Ok(response.Event) : Results.NoContent();
    }
}
