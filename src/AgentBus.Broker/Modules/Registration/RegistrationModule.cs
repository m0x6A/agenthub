using AgentBus.Broker.Modules.Registration.Features.RegisterAgent;
using AgentBus.Broker.Modules.Registration.Features.DiscoverAgents;
using AgentBus.Broker.Modules.Registration.Features.GetAgent;
using AgentBus.Broker.Modules.Registration.Features.UpdateHeartbeat;
using AgentBus.Broker.Modules.Registration.Features.DeregisterAgent;

namespace AgentBus.Broker.Modules.Registration;

public static class RegistrationModule
{
    public static IServiceCollection AddRegistrationModule(this IServiceCollection services)
    {
        // Register handlers
        services.AddScoped<RegisterAgentHandler>();
        services.AddScoped<DiscoverAgentsHandler>();
        services.AddScoped<GetAgentHandler>();
        services.AddScoped<UpdateHeartbeatHandler>();
        services.AddScoped<DeregisterAgentHandler>();

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

        return app;
    }
}
