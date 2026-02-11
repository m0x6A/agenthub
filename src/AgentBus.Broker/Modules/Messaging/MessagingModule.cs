using AgentBus.Broker.Modules.Messaging.Features.SendMessage;
using AgentBus.Broker.Modules.Messaging.Features.ReceiveMessage;
using AgentBus.Broker.Modules.Messaging.Features.AcknowledgeMessage;
using AgentBus.Broker.SharedKernel.Interfaces;

namespace AgentBus.Broker.Modules.Messaging;

public static class MessagingModule
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services)
    {
        services.AddScoped<SendMessageHandler>();
        services.AddScoped<ReceiveMessageHandler>();
        services.AddScoped<AcknowledgeMessageHandler>();
        services.AddSingleton<IMessageBroker, ServiceBusMessageBroker>();
        return services;
    }

    public static WebApplication MapMessagingEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/messages/send", SendMessageEndpoint.Handle)
            .WithName("SendMessage")
            .WithTags("Messaging")
            .AllowAnonymous(); // Allow anonymous for development

        app.MapGet("/api/v1/messages/receive", ReceiveMessageEndpoint.Handle)
            .WithName("ReceiveMessage")
            .WithTags("Messaging")
            .AllowAnonymous(); // Allow anonymous for development

        app.MapPost("/api/v1/messages/{messageId}/ack", AcknowledgeMessageEndpoint.Handle)
            .WithName("AcknowledgeMessage")
            .WithTags("Messaging")
            .AllowAnonymous(); // Allow anonymous for development

        return app;
    }
}
