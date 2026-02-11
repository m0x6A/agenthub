using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgentBus.Broker.Modules.Messaging.Features.ReceiveMessage;

public static class ReceiveMessageEndpoint
{
    public static async Task<IResult> Handle(
        [FromServices] ReceiveMessageHandler handler,
        ClaimsPrincipal user,
        CancellationToken cancellationToken,
        [FromQuery] int timeout = 30)
    {
        var agentId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(agentId))
        {
            return Results.Unauthorized();
        }

        var response = await handler.HandleAsync(agentId, timeout, cancellationToken);
        return response.Message != null ? Results.Ok(response.Message) : Results.NoContent();
    }
}
