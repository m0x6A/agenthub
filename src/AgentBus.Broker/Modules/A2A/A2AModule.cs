using A2A;
using A2A.AspNetCore;
using AgentBus.Broker.SharedKernel.A2A;

namespace AgentBus.Broker.Modules.A2A;

public static class A2AModule
{
    public static IServiceCollection AddA2AModule(this IServiceCollection services)
    {
        // Register A2A TaskStore
        services.AddSingleton<ITaskStore, CosmosDbTaskStore>();
        
        // Register TaskManager (A2A's TaskManager)
        services.AddSingleton<ITaskManager>(sp =>
        {
            var taskStore = sp.GetRequiredService<ITaskStore>();
            return new TaskManager(taskStore: taskStore);
        });

        return services;
    }

    public static IEndpointRouteBuilder MapA2AEndpoints(this IEndpointRouteBuilder app)
    {
        var taskManager = app.ServiceProvider.GetRequiredService<ITaskManager>();

        // Map A2A JSON-RPC endpoints at broker level
        // This allows the broker itself to act as an A2A agent for service discovery
        app.MapA2A(taskManager, "/a2a/broker");
        app.MapWellKnownAgentCard(taskManager, "/a2a/broker");

        // Configure broker agent card
        taskManager.OnAgentCardQuery = (agentUrl, ct) =>
        {
            return Task.FromResult(new AgentCard
            {
                Name = "AgentBus Service Discovery Broker",
                Description = "Centralized service discovery and event routing broker for agent-to-agent communication",
                Url = agentUrl,
                Version = "1.0.0",
                ProtocolVersion = "0.3.0",
                DefaultInputModes = ["application/json"],
                DefaultOutputModes = ["application/json"],
                Capabilities = new AgentCapabilities
                {
                    Streaming = false,
                    PushNotifications = false,
                    StateTransitionHistory = false
                },
                Skills =
                [
                    new AgentSkill
                    {
                        Id = "agent-discovery",
                        Name = "Agent Discovery",
                        Description = "Discover registered agents by capability",
                        Tags = ["discovery", "registry", "service-discovery"],
                        Examples = ["Find agents with capability 'schedule-drone'"]
                    },
                    new AgentSkill
                    {
                        Id = "event-subscription",
                        Name = "Event Subscription Management",
                        Description = "Subscribe to and publish domain events",
                        Tags = ["events", "pubsub", "messaging"],
                        Examples = ["Subscribe to 'package-ready' events"]
                    }
                ]
            });
        };

        return app;
    }
}
