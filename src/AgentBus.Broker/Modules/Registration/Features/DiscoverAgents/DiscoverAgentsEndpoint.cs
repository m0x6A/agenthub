using Microsoft.AspNetCore.Mvc;

namespace AgentBus.Broker.Modules.Registration.Features.DiscoverAgents;

public static class DiscoverAgentsEndpoint
{
    public static IEndpointRouteBuilder MapDiscoverAgentsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/agents", DiscoverAgents)
            .WithName("DiscoverAgents")
            .WithTags("Registration")
            .RequireAuthorization();
            //.WithOpenApi();

        return app;
    }

    private static async Task<IResult> DiscoverAgents(
        [FromQuery] string? capability,
        [FromQuery] string? status,
        DiscoverAgentsHandler handler,
        CancellationToken cancellationToken)
    {
        var statusEnum = status != null && Enum.TryParse<SharedKernel.Models.AgentStatus>(status, true, out var s) 
            ? s 
            : (SharedKernel.Models.AgentStatus?)null;

        var request = new DiscoverAgentsRequest(capability, statusEnum);
        var response = await handler.HandleAsync(request, cancellationToken);
        
        return Results.Ok(response);
    }
}
