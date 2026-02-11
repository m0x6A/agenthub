using AgentBus.Broker.Modules.Registration.Features.RegisterAgent;
using AgentBus.Broker.Modules.Registration.Features.DiscoverAgents;
using AgentBus.Broker.Modules.Registration.Features.GetAgent;
using AgentBus.Broker.Modules.Registration.Features.UpdateHeartbeat;
using AgentBus.Broker.Modules.Registration.Features.DeregisterAgent;
using AgentBus.Broker.Modules.Registration.Features.SubscribeToEvent;
using AgentBus.Broker.Modules.Registration.Features.UnsubscribeFromEvent;
using AgentBus.Broker.SharedKernel.Interfaces;

namespace AgentBus.Broker.Modules.Registration;

public static class RegistrationModule
{
    public static IServiceCollection AddRegistrationModule(this IServiceCollection services)
    {
        // Register infrastructure
        services.AddSingleton<IAgentRegistry, CosmosDbAgentRegistry>();

        // Register handlers
        services.AddScoped<RegisterAgentHandler>();
        services.AddScoped<DiscoverAgentsHandler>();
        services.AddScoped<GetAgentHandler>();
        services.AddScoped<UpdateHeartbeatHandler>();
        services.AddScoped<DeregisterAgentHandler>();
        services.AddScoped<SubscribeToEventHandler>();
        services.AddScoped<UnsubscribeFromEventHandler>();

        // Register validators
        services.AddScoped<RegisterAgentValidator>();

        // Register TimeProvider
        services.AddSingleton(TimeProvider.System);

        return services;
    }

    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapRegisterAgentEndpoint();
        app.MapDiscoverAgentsEndpoint();
        app.MapGetAgentEndpoint();
        app.MapUpdateHeartbeatEndpoint();
        app.MapDeregisterAgentEndpoint();
        app.MapSubscribeToEventEndpoint();
        app.MapUnsubscribeFromEventEndpoint();

        return app;
    }
}
