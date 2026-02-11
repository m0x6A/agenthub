using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.Modules.Registration.Features.RegisterAgent;

public sealed record RegisterAgentRequest(
    string Id,
    string Name,
    string Version,
    string[] Capabilities,
    MessageTypes MessageTypes,
    AgentIdentity Identity,
    AgentMetadata? Metadata,
    string? HealthCheckUrl = null);
