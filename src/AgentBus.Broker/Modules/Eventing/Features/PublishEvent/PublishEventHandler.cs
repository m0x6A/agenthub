using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using System.Text.Json;

namespace AgentBus.Broker.Modules.Eventing.Features.PublishEvent;

public sealed class PublishEventHandler(
    IEventBroker eventBroker,
    ILogger<PublishEventHandler> logger)
{
    public async Task<PublishEventResponse> HandleAsync(
        PublishEventRequest request,
        string sourceAgentId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Publishing event {EventType} from {Source}", request.EventType, sourceAgentId);

        var envelope = new EventEnvelope(
            EventId: Guid.NewGuid().ToString(),
            EventType: request.EventType,
            Source: sourceAgentId,
            Timestamp: DateTime.UtcNow,
            DataVersion: request.DataVersion,
            Data: JsonSerializer.SerializeToElement(request.Data),
            Headers: request.Headers);

        var eventId = await eventBroker.PublishEventAsync(envelope, cancellationToken);
        
        return new PublishEventResponse(eventId);
    }
}
