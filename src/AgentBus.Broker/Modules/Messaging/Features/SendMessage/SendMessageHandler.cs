using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using System.Text.Json;

namespace AgentBus.Broker.Modules.Messaging.Features.SendMessage;

public sealed class SendMessageHandler(
    IAgentRegistry agentRegistry,
    IMessageBroker messageBroker,
    ILogger<SendMessageHandler> logger)
{
    public async Task<SendMessageResponse> HandleAsync(
        SendMessageRequest request,
        string fromAgentId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Sending message from {From} to {To}", fromAgentId, request.To);

        var toAgent = await agentRegistry.GetAgentByIdAsync(request.To, cancellationToken);
        if (toAgent == null)
        {
            throw new InvalidOperationException($"Recipient agent {request.To} not found");
        }

        var envelope = new MessageEnvelope(
            MessageId: Guid.NewGuid().ToString(),
            CorrelationId: request.CorrelationId,
            ConversationId: request.ConversationId,
            From: fromAgentId,
            To: request.To,
            MessageType: request.MessageType,
            Timestamp: DateTime.UtcNow,
            Ttl: request.TtlSeconds.HasValue ? TimeSpan.FromSeconds(request.TtlSeconds.Value) : TimeSpan.FromHours(24),
            Payload: JsonSerializer.SerializeToElement(request.Payload),
            ReplyTo: $"agent-{fromAgentId}-inbox",
            Headers: request.Headers);

        var messageId = await messageBroker.SendMessageAsync(envelope, cancellationToken);
        
        return new SendMessageResponse(messageId);
    }
}
