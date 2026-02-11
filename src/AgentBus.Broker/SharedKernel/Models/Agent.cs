namespace AgentBus.Broker.SharedKernel.Models;

public sealed record Agent(
    string Id,
    string PartitionKey,
    string Name,
    string Version,
    AgentStatus Status,
    string[] Capabilities,
    MessageTypes MessageTypes,
    AgentIdentity Identity,
    AgentEndpoints Endpoints,
    CommunicationCapabilities Communication,
    EventSubscription[] EventSubscriptions,
    string[] EventsPublished,
    TransportPreference PreferredTransport,
    AgentMetadata? Metadata,
    AgentTimestamps Timestamps);

public enum AgentStatus
{
    Active,
    Inactive
}

public sealed record MessageTypes(string[] Accepts, string[] Emits);

public sealed record AgentIdentity(
    string ManagedIdentityId,
    string PrincipalId,
    string TenantId);

public sealed record AgentEndpoints(
    string? InboxQueueName,
    string? HealthCheckUrl,
    string A2AEndpointUrl);

public sealed record CommunicationCapabilities(
    bool SupportsA2ADirect,
    bool SupportsEventPublishing,
    bool SupportsEventSubscription);

public sealed record EventSubscription(
    string EventType,
    string SubscriptionId,
    Dictionary<string, string>? Filters);

public sealed record AgentMetadata(
    string? Owner,
    string? Environment,
    string[]? Tags);

public sealed record AgentTimestamps(
    DateTime RegisteredAt,
    DateTime LastHeartbeat);
