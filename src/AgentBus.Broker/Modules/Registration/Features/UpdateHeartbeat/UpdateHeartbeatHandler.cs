using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.Modules.Registration.Features.UpdateHeartbeat;

public sealed class UpdateHeartbeatHandler
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly TimeProvider _timeProvider;

    public UpdateHeartbeatHandler(IAgentRegistry agentRegistry, TimeProvider timeProvider)
    {
        _agentRegistry = agentRegistry;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(UpdateHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var agent = await _agentRegistry.GetAgentByIdAsync(request.AgentId, cancellationToken);
        
        if (agent is null)
        {
            throw new InvalidOperationException($"Agent with ID '{request.AgentId}' not found");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        
        // Update heartbeat timestamp
        await _agentRegistry.UpdateHeartbeatAsync(request.AgentId, now, cancellationToken);

        // Reactivate if inactive
        if (agent.Status == AgentStatus.Inactive)
        {
            await _agentRegistry.UpdateStatusAsync(request.AgentId, AgentStatus.Active, cancellationToken);
        }
    }
}
