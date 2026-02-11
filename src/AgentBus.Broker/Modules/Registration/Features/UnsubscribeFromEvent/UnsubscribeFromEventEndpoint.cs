namespace AgentBus.Broker.Modules.Registration.Features.UnsubscribeFromEvent;

public static class UnsubscribeFromEventEndpoint
{
    public static IEndpointRouteBuilder MapUnsubscribeFromEventEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/agents/{agentId}/subscriptions/{subscriptionId}", UnsubscribeFromEvent)
            .WithName("UnsubscribeByAgentId")
            .WithTags("Registration", "Events")
            .AllowAnonymous(); // Allow anonymous for development

        return app;
    }

    private static async Task<IResult> UnsubscribeFromEvent(
        string agentId,
        string subscriptionId,
        UnsubscribeFromEventHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            await handler.HandleAsync(agentId, subscriptionId, cancellationToken);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }
}
