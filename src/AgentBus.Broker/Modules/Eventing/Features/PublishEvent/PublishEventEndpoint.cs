using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgentBus.Broker.Modules.Eventing.Features.PublishEvent;

public static class PublishEventEndpoint
{
    public static async Task<IResult> Handle(
        [FromBody] PublishEventRequest request,
        [FromServices] PublishEventHandler handler,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        try
        {
            var agentId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(agentId))
            {
                return Results.Unauthorized();
            }

            var response = await handler.HandleAsync(request, agentId, cancellationToken);
            return Results.Accepted($"/api/v1/events/{response.EventId}", response);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: 500);
        }
    }
}
