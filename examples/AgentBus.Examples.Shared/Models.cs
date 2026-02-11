using System.Text.Json;

namespace AgentBus.Examples.Shared;

public record EventEnvelope(
    string EventId,
    string EventType,
    string Source,
    DateTime Timestamp,
    string DataVersion,
    JsonElement Data,
    Dictionary<string, string>? Headers);

public record Subscription(
    string SubscriptionId,
    string TopicName,
    string AgentId,
    string? EventType);

public record AgentRegistration(
    string Id,
    string Name,
    string Version,
    string[] Capabilities,
    MessageTypes MessageTypes,
    CommunicationCapabilities Communication,
    string[] EventsPublished,
    AgentMetadata? Metadata);

public record MessageTypes(string[] Accepts, string[] Emits);

public record CommunicationCapabilities(
    bool SupportsA2ADirect,
    bool SupportsEventPublishing,
    bool SupportsEventSubscription);

public record AgentMetadata(
    string? Owner,
    string? Environment,
    string[]? Tags);
