namespace AgentBus.Broker.Modules.Eventing.Features.PublishEvent;

public sealed record PublishEventRequest(
    string EventType,
    string DataVersion,
    object Data,
    Dictionary<string, string>? Headers = null);
