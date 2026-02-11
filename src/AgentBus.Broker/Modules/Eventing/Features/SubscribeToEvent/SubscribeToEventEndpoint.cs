using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgentBus.Broker.Modules.Eventing.Features.SubscribeToEvent;

public static class SubscribeToEventEndpoint
{
    public static async Task<IResult> Handle(
        [FromBody] SubscribeToEventRequest request,
        [FromServices] SubscribeToEventHandler handler,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var agentId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(agentId))
        {
            return Results.Unauthorized();
        }

        var response = await handler.HandleAsync(request, agentId, cancellationToken);
        return Results.Created($"/api/v1/events/subscriptions/{response.Subscription.Id}", response.Subscription);
    }
}
