namespace AgentBus.Broker.Modules.Eventing.Features.SubscribeToEvent;

public sealed record SubscribeToEventRequest(
    string EventType,
    string[]? Filters = null);
