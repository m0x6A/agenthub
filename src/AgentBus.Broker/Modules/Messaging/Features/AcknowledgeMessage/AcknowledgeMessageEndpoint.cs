using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgentBus.Broker.Modules.Messaging.Features.AcknowledgeMessage;

public static class AcknowledgeMessageEndpoint
{
    public static async Task<IResult> Handle(
        [FromRoute] string messageId,
        [FromServices] AcknowledgeMessageHandler handler,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var agentId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(agentId))
        {
            return Results.Unauthorized();
        }

        await handler.HandleAsync(agentId, messageId, cancellationToken);
        return Results.NoContent();
    }
}
