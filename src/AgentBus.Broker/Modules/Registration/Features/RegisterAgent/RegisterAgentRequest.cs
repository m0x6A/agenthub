using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.Modules.Registration.Features.RegisterAgent;

public sealed record RegisterAgentRequest(
    string Id,
    string Name,
    string Version,
    string[] Capabilities,
    MessageTypes MessageTypes,
    AgentIdentity Identity,
    string A2AEndpointUrl,
    CommunicationCapabilities Communication,
    string[] EventsPublished,
    TransportPreference PreferredTransport,
    AgentMetadata? Metadata,
    string? HealthCheckUrl = null,
    string? InboxQueueName = null);
