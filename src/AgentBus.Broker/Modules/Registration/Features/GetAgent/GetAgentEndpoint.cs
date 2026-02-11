using Microsoft.AspNetCore.Mvc;

namespace AgentBus.Broker.Modules.Registration.Features.GetAgent;

public static class GetAgentEndpoint
{
    public static IEndpointRouteBuilder MapGetAgentEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/agents/{agentId}", GetAgent)
            .WithName("GetAgent")
            .WithTags("Registration")
            .RequireAuthorization();
            //.WithOpenApi();

        return app;
    }

    private static async Task<IResult> GetAgent(
        [FromRoute] string agentId,
        GetAgentHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var agent = await handler.HandleAsync(new GetAgentRequest(agentId), cancellationToken);
            
            if (agent is null)
            {
                return Results.NotFound(new { error = $"Agent with ID '{agentId}' not found" });
            }
            
            return Results.Ok(agent);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }
}
