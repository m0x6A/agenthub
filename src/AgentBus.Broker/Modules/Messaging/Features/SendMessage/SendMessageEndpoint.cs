using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgentBus.Broker.Modules.Messaging.Features.SendMessage;

public static class SendMessageEndpoint
{
    public static async Task<IResult> Handle(
        [FromBody] SendMessageRequest request,
        [FromServices] SendMessageHandler handler,
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
            return Results.Accepted($"/api/v1/messages/{response.MessageId}", response);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: 500);
        }
    }
}
