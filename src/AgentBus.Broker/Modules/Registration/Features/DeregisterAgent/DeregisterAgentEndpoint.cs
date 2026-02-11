using Microsoft.AspNetCore.Mvc;

namespace AgentBus.Broker.Modules.Registration.Features.DeregisterAgent;

public static class DeregisterAgentEndpoint
{
    public static IEndpointRouteBuilder MapDeregisterAgentEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/agents/{agentId}", DeregisterAgent)
            .WithName("DeregisterAgent")
            .WithTags("Registration")
            .AllowAnonymous(); // Allow anonymous for development
            //.WithOpenApi();

        return app;
    }

    private static async Task<IResult> DeregisterAgent(
        [FromRoute] string agentId,
        DeregisterAgentHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            await handler.HandleAsync(new DeregisterAgentRequest(agentId), cancellationToken);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }
}
