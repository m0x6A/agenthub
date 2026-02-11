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
        
        const int maxRetries = 10;
        var retryDelay = TimeSpan.FromSeconds(2);
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                logger.LogInformation("Verifying Service Bus reply queue '{QueueName}' (attempt {Attempt}/{MaxRetries})...", 
                    replyQueueName, attempt, maxRetries);
                    
                var sender = serviceBusClient.CreateSender(replyQueueName);
                await sender.DisposeAsync(); // Just testing connection
                logger.LogInformation("✅ Reply queue '{QueueName}' is available", replyQueueName);
                return app; // Success!
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex,
                    "Failed to connect to Service Bus (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}s...",
                    attempt, maxRetries, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay);
                retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 1.5); // Exponential backoff
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "❌ Failed to connect to Service Bus after {MaxRetries} attempts. Reply queue '{QueueName}' may not exist. Create it manually or ensure Service Bus has auto-create permissions.",
                    maxRetries, replyQueueName);
                // Don't throw - allow startup to continue, queue will be created on first use if admin permissions available
                return app;
            }
        }

        return app;
    }
}
