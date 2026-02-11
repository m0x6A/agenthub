using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.Modules.Registration.Features.RegisterAgent;

public sealed class RegisterAgentHandler
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IMessageBroker _messageBroker;
    private readonly TimeProvider _timeProvider;

    public RegisterAgentHandler(
        IAgentRegistry agentRegistry,
        IMessageBroker messageBroker,
        TimeProvider timeProvider)
    {
        _agentRegistry = agentRegistry;
        _messageBroker = messageBroker;
        _timeProvider = timeProvider;
    }

    public async Task<Agent> HandleAsync(RegisterAgentRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var inboxQueueName = GenerateInboxQueueName(request.Id);

        var agent = new Agent(
            Id: request.Id,
            PartitionKey: request.Id,
            Name: request.Name,
            Version: request.Version,
            Status: AgentStatus.Active,
            Capabilities: request.Capabilities,
            MessageTypes: request.MessageTypes,
            Identity: request.Identity,
            Endpoints: new AgentEndpoints(
                InboxQueueName: inboxQueueName,
                HealthCheckUrl: request.HealthCheckUrl
            ),
            Metadata: request.Metadata,
            Timestamps: new AgentTimestamps(
                RegisteredAt: now,
                LastHeartbeat: now
            )
        );

        // Create inbox queue first (using agent ID per interface)
        await _messageBroker.CreateInboxQueueAsync(request.Id, cancellationToken);

        // Register agent in Cosmos DB
        await _agentRegistry.RegisterAgentAsync(agent, cancellationToken);

        return agent;
    }

    private static string GenerateInboxQueueName(string agentId)
    {
        return $"agent-{agentId}-inbox";
    }
}
