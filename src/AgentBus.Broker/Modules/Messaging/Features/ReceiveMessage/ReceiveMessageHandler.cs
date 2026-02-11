using AgentBus.Broker.SharedKernel.Interfaces;

namespace AgentBus.Broker.Modules.Messaging.Features.ReceiveMessage;

public sealed class ReceiveMessageHandler(
    IMessageBroker messageBroker,
    ILogger<ReceiveMessageHandler> logger)
{
    public async Task<ReceiveMessageResponse> HandleAsync(
        string agentId,
        int timeout,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Agent {AgentId} receiving messages with timeout {Timeout}s", agentId, timeout);
        
        var message = await messageBroker.ReceiveMessageAsync(agentId, timeout, cancellationToken);
        return new ReceiveMessageResponse(message);
    }
}
