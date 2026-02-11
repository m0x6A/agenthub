using AgentBus.Broker.SharedKernel.Interfaces;

namespace AgentBus.Broker.Modules.Messaging.Features.AcknowledgeMessage;

public sealed class AcknowledgeMessageHandler(
    IMessageBroker messageBroker,
    ILogger<AcknowledgeMessageHandler> logger)
{
    public async Task HandleAsync(
        string agentId,
        string messageId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Agent {AgentId} acknowledging message {MessageId}", agentId, messageId);
        await messageBroker.AcknowledgeMessageAsync(agentId, messageId, cancellationToken);
    }
}
