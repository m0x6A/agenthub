using System.Text.Json;

namespace AgentBus.Broker.SharedKernel.Models;

public sealed record EventEnvelope(
    string EventId,
    string EventType,
    string Source,
    DateTime Timestamp,
    string DataVersion,
    JsonElement Data,
    Dictionary<string, string>? Headers);
