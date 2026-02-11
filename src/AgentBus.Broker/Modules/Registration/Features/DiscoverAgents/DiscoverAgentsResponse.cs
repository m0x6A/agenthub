using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.Modules.Registration.Features.DiscoverAgents;

public sealed record DiscoverAgentsResponse(
    Agent[] Agents,
    int TotalCount);
