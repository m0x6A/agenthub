using System.Text.Json;

namespace AgentBus.Broker.SharedKernel.Models;

public sealed record MessageEnvelope(
    string MessageId,
    string? CorrelationId,
    string? ConversationId,
    string From,
    string To,
    string MessageType,
    DateTime Timestamp,
    TimeSpan? Ttl,
    JsonElement Payload,
    string? ReplyTo,
    Dictionary<string, string>? Headers);
