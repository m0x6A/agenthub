using Microsoft.AspNetCore.Mvc;

namespace AgentBus.Broker.Modules.Registration.Features.SubscribeToEvent;

public sealed record SubscribeToEventRequest(
    string EventType,
    Dictionary<string, string>? Filters = null);

public static class SubscribeToEventEndpoint
{
    public static IEndpointRouteBuilder MapSubscribeToEventEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/agents/{agentId}/subscriptions", SubscribeToEvent)
            .WithName("SubscribeToEvent")
            .WithTags("Registration", "Events")
            .AllowAnonymous(); // Allow anonymous for development

        return app;
    }

    private static async Task<IResult> SubscribeToEvent(
        string agentId,
        [FromBody] SubscribeToEventRequest request,
        SubscribeToEventHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var subscription = await handler.HandleAsync(agentId, request, cancellationToken);
            return Results.Created(
                $"/api/v1/agents/{agentId}/subscriptions/{subscription.SubscriptionId}",
                subscription);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }
}
