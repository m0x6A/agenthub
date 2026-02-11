using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace AgentBus.Broker.Modules.Registration;

public sealed class CosmosDbAgentRegistry : IAgentRegistry
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbAgentRegistry> _logger;

    public CosmosDbAgentRegistry(CosmosClient cosmosClient, IConfiguration configuration, ILogger<CosmosDbAgentRegistry> logger)
    {
        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "agentbus";
        var containerName = configuration["CosmosDb:AgentsContainerName"] ?? "agents";
        
        _container = cosmosClient.GetContainer(databaseName, containerName);
        _logger = logger;
    }

    public async Task<Agent?> GetAgentByIdAsync(string agentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<Agent>(
                agentId, 
                new PartitionKey(agentId),
                cancellationToken: cancellationToken);
            
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<Agent>> FindAgentsByCapabilityAsync(string capability, CancellationToken cancellationToken = default)
    {
        var query = _container.GetItemLinqQueryable<Agent>()
            .Where(a => a.Capabilities.Contains(capability))
            .ToFeedIterator();

        var agents = new List<Agent>();
        
        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync(cancellationToken);
            agents.AddRange(response);
        }

        return agents;
    }

    public async Task<IEnumerable<Agent>> GetAllAgentsAsync(AgentStatus? status = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Agent> queryable = _container.GetItemLinqQueryable<Agent>();
        
        if (status.HasValue)
        {
            queryable = queryable.Where(a => a.Status == status.Value);
        }

        var query = queryable.ToFeedIterator();
        var agents = new List<Agent>();
        
        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync(cancellationToken);
            agents.AddRange(response);
        }

        return agents;
    }

    public async Task<Agent> RegisterAgentAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.CreateItemAsync(
                agent,
                new PartitionKey(agent.PartitionKey),
                cancellationToken: cancellationToken);
            
            _logger.LogInformation("Agent {AgentId} registered successfully", agent.Id);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException($"Agent with ID '{agent.Id}' already exists", ex);
        }
    }

    public async Task UpdateHeartbeatAsync(string agentId, DateTime heartbeatTime, CancellationToken cancellationToken = default)
    {
        var patchOperations = new[]
        {
            PatchOperation.Set("/timestamps/lastHeartbeat", heartbeatTime)
        };

        await _container.PatchItemAsync<Agent>(
            agentId,
            new PartitionKey(agentId),
            patchOperations,
            cancellationToken: cancellationToken);
        
        _logger.LogDebug("Heartbeat updated for agent {AgentId} at {HeartbeatTime}", agentId, heartbeatTime);
    }

    public async Task UpdateStatusAsync(string agentId, AgentStatus status, CancellationToken cancellationToken = default)
    {
        var patchOperations = new[]
        {
            PatchOperation.Set("/status", status.ToString().ToLower())
        };

        await _container.PatchItemAsync<Agent>(
            agentId,
            new PartitionKey(agentId),
            patchOperations,
            cancellationToken: cancellationToken);
        
        _logger.LogInformation("Agent {AgentId} status updated to {Status}", agentId, status);
    }

    public async Task DeleteAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await _container.DeleteItemAsync<Agent>(
            agentId,
            new PartitionKey(agentId),
            cancellationToken: cancellationToken);
        
        _logger.LogInformation("Agent {AgentId} deleted from registry", agentId);
    }

    public async Task AddEventSubscriptionAsync(string agentId, EventSubscription subscription, CancellationToken cancellationToken = default)
    {
        var agent = await GetAgentByIdAsync(agentId, cancellationToken);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent {agentId} not found");
        }

        var updatedSubscriptions = agent.EventSubscriptions.Append(subscription).ToArray();

        var patchOperations = new[]
        {
            PatchOperation.Set("/eventSubscriptions", updatedSubscriptions)
        };

        await _container.PatchItemAsync<Agent>(
            agentId,
            new PartitionKey(agentId),
            patchOperations,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Added event subscription {SubscriptionId} for agent {AgentId}", 
            subscription.SubscriptionId, agentId);
    }

    public async Task RemoveEventSubscriptionAsync(string agentId, string subscriptionId, CancellationToken cancellationToken = default)
    {
        var agent = await GetAgentByIdAsync(agentId, cancellationToken);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent {agentId} not found");
        }

        var updatedSubscriptions = agent.EventSubscriptions
            .Where(s => s.SubscriptionId != subscriptionId)
            .ToArray();

        var patchOperations = new[]
        {
            PatchOperation.Set("/eventSubscriptions", updatedSubscriptions)
        };

        await _container.PatchItemAsync<Agent>(
            agentId,
            new PartitionKey(agentId),
            patchOperations,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Removed event subscription {SubscriptionId} from agent {AgentId}", 
            subscriptionId, agentId);
    }

    public async Task<IEnumerable<EventSubscription>> GetEventSubscriptionsAsync(string agentId, CancellationToken cancellationToken = default)
    {
        var agent = await GetAgentByIdAsync(agentId, cancellationToken);
        return agent?.EventSubscriptions ?? Array.Empty<EventSubscription>();
    }
}
