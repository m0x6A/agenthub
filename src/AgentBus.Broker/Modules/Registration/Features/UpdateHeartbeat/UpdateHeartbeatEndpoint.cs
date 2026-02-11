using Microsoft.AspNetCore.Mvc;

namespace AgentBus.Broker.Modules.Registration.Features.UpdateHeartbeat;

public static class UpdateHeartbeatEndpoint
{
    public static IEndpointRouteBuilder MapUpdateHeartbeatEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/v1/agents/{agentId}/heartbeat", UpdateHeartbeat)
            .WithName("UpdateHeartbeat")
            .WithTags("Registration")
            .RequireAuthorization();
            //.WithOpenApi();

        return app;
    }

    private static async Task<IResult> UpdateHeartbeat(
        [FromRoute] string agentId,
        UpdateHeartbeatHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            await handler.HandleAsync(new UpdateHeartbeatRequest(agentId), cancellationToken);
            return Results.Ok(new { message = "Heartbeat updated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }
}
