using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.Modules.Registration.Features.GetAgent;

public sealed class GetAgentHandler
{
    private readonly IAgentRegistry _agentRegistry;

    public GetAgentHandler(IAgentRegistry agentRegistry)
    {
        _agentRegistry = agentRegistry;
    }

    public async Task<Agent?> HandleAsync(GetAgentRequest request, CancellationToken cancellationToken)
    {
        return await _agentRegistry.GetAgentByIdAsync(request.AgentId, cancellationToken);
    }
}
