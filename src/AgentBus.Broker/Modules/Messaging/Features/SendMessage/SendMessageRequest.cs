namespace AgentBus.Broker.Modules.Messaging.Features.SendMessage;

public sealed record SendMessageRequest(
    string To,
    string MessageType,
    object Payload,
    string? CorrelationId = null,
    string? ConversationId = null,
    int? TtlSeconds = null,
    Dictionary<string, string>? Headers = null);
