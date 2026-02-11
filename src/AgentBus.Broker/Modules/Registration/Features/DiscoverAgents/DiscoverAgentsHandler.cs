using AgentBus.Broker.SharedKernel.Interfaces;

namespace AgentBus.Broker.Modules.Registration.Features.DiscoverAgents;

public sealed class DiscoverAgentsHandler
{
    private readonly IAgentRegistry _agentRegistry;

    public DiscoverAgentsHandler(IAgentRegistry agentRegistry)
    {
        _agentRegistry = agentRegistry;
    }

    public async Task<DiscoverAgentsResponse> HandleAsync(
        DiscoverAgentsRequest request,
        CancellationToken cancellationToken)
    {
        var agentsEnumerable = request.Capability != null
            ? await _agentRegistry.FindAgentsByCapabilityAsync(request.Capability, cancellationToken)
            : await _agentRegistry.GetAllAgentsAsync(request.Status, cancellationToken);

        var agents = agentsEnumerable.ToArray();

        return new DiscoverAgentsResponse(
            Agents: agents,
            TotalCount: agents.Length
        );
    }
}
