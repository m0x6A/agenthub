using AgentBus.Broker.Modules.Eventing.Features.PublishEvent;
using AgentBus.Broker.Modules.Eventing.Features.SubscribeToEvent;
using AgentBus.Broker.Modules.Eventing.Features.ReceiveEvent;
using AgentBus.Broker.Modules.Eventing.Features.UnsubscribeFromEvent;
using AgentBus.Broker.SharedKernel.Interfaces;

namespace AgentBus.Broker.Modules.Eventing;

public static class EventingModule
{
    public static IServiceCollection AddEventingModule(this IServiceCollection services)
    {
        services.AddScoped<PublishEventHandler>();
        services.AddScoped<SubscribeToEventHandler>();
        services.AddScoped<ReceiveEventHandler>();
        services.AddScoped<UnsubscribeFromEventHandler>();
        services.AddSingleton<IEventBroker, ServiceBusEventBroker>();
        return services;
    }

    public static WebApplication MapEventingEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/events/publish", PublishEventEndpoint.Handle)
            .WithName("PublishEvent")
            .WithTags("Eventing")
            .RequireAuthorization();

        app.MapPost("/api/v1/events/subscribe", SubscribeToEventEndpoint.Handle)
            .WithName("SubscribeToEvent")
            .WithTags("Eventing")
            .RequireAuthorization();

        app.MapGet("/api/v1/events/receive/{subscriptionId}", ReceiveEventEndpoint.Handle)
            .WithName("ReceiveEvent")
            .WithTags("Eventing")
            .RequireAuthorization();

        app.MapDelete("/api/v1/events/subscriptions/{subscriptionId}", UnsubscribeFromEventEndpoint.Handle)
            .WithName("UnsubscribeFromEvent")
            .WithTags("Eventing")
            .RequireAuthorization();

        return app;
    }
}
