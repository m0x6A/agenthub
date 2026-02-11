using Microsoft.AspNetCore.Mvc;
using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.Modules.Registration.Features.RegisterAgent;

public static class RegisterAgentEndpoint
{
    public static IEndpointRouteBuilder MapRegisterAgentEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/agents", RegisterAgent)
            .WithName("RegisterAgent")
            .WithTags("Registration")
            .RequireAuthorization();
            //.WithOpenApi();

        return app;
    }

    private static async Task<IResult> RegisterAgent(
        [FromBody] RegisterAgentRequest request,
        RegisterAgentHandler handler,
        RegisterAgentValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        try
        {
            var agent = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/v1/agents/{agent.Id}", agent);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }
}
