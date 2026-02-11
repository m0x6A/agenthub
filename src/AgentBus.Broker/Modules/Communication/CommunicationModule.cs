using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Services;
using AgentBus.Broker.SharedKernel.Transport;
using AgentBus.Broker.Modules.Communication.Features.SendMessage;

namespace AgentBus.Broker.Modules.Communication;

/// <summary>
/// Module for agent-to-agent communication infrastructure.
/// </summary>
public static class CommunicationModule
{
    public static IServiceCollection AddCommunicationModule(this IServiceCollection services)
    {
        // Register transports
        services.AddSingleton<IAgentTransport, ServiceBusAgentTransport>();
        services.AddSingleton<IAgentTransport, HttpAgentTransport>();
        
        // Register communication service
        services.AddSingleton<AgentCommunicationService>();
        
        // Add HTTP client factory for A2A client
        services.AddHttpClient();

        return services;
    }

    public static IEndpointRouteBuilder MapCommunicationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapSendMessageEndpoint();
        return app;
    }

    public static async Task<IApplicationBuilder> InitializeCommunicationModuleAsync(this IApplicationBuilder app)
    {
        // Ensure reply queue exists for Service Bus transport
        var serviceBusClient = app.ApplicationServices.GetRequiredService<Azure.Messaging.ServiceBus.ServiceBusClient>();
        var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
        var logger = app.ApplicationServices.GetRequiredService<ILogger<ServiceBusAgentTransport>>();
        
        var replyQueueName = configuration["ServiceBus:ReplyQueueName"] ?? "agent-replies";
        
        try
        {
            var sender = serviceBusClient.CreateSender(replyQueueName);
            await sender.DisposeAsync(); // Just testing connection
            logger.LogInformation("Reply queue '{QueueName}' is available", replyQueueName);
        }
        catch
        {
            logger.LogWarning(
                "Reply queue '{QueueName}' may not exist. Create it manually or ensure Service Bus has auto-create permissions.",
                replyQueueName);
        }

        return app;
    }
}
