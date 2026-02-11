using A2A;
using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.SharedKernel.Transport;

/// <summary>
/// HTTP transport implementation using A2A protocol for agent communication.
/// </summary>
public sealed class HttpAgentTransport : IAgentTransport
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpAgentTransport> _logger;

    public TransportType Type => TransportType.Http;

    public HttpAgentTransport(
        IAgentRegistry agentRegistry,
        IHttpClientFactory httpClientFactory,
        ILogger<HttpAgentTransport> logger)
    {
        _agentRegistry = agentRegistry;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<AgentMessage> SendMessageAsync(
        string targetAgentId,
        MessageSendParams message,
        CancellationToken cancellationToken = default)
    {
        var agent = await _agentRegistry.GetAgentByIdAsync(targetAgentId, cancellationToken);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent '{targetAgentId}' not found");
        }

        if (string.IsNullOrEmpty(agent.Endpoints.A2AEndpointUrl))
        {
            throw new InvalidOperationException($"Agent '{targetAgentId}' does not have an A2A endpoint configured");
        }

        var client = new A2AClient(new Uri(agent.Endpoints.A2AEndpointUrl), _httpClientFactory.CreateClient());

        _logger.LogInformation(
            "Sending message to agent {AgentId} via HTTP at {Endpoint}",
            targetAgentId, agent.Endpoints.A2AEndpointUrl);

        var response = await client.SendMessageAsync(message, cancellationToken);

        _logger.LogInformation(
            "Received response from agent {AgentId}",
            targetAgentId);

        // Convert A2AResponse to AgentMessage
        // A2AResponse structure mirrors AgentMessage but may have different property names
        return (AgentMessage)(object)response;
    }

    public async IAsyncEnumerable<AgentMessage> StreamMessagesAsync(
        string targetAgentId,
        MessageSendParams message,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var agent = await _agentRegistry.GetAgentByIdAsync(targetAgentId, cancellationToken);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent '{targetAgentId}' not found");
        }

        if (string.IsNullOrEmpty(agent.Endpoints.A2AEndpointUrl))
        {
            throw new InvalidOperationException($"Agent '{targetAgentId}' does not have an A2A endpoint configured");
        }

        var client = new A2AClient(new Uri(agent.Endpoints.A2AEndpointUrl), _httpClientFactory.CreateClient());

        _logger.LogInformation(
            "Starting message stream to agent {AgentId} via HTTP at {Endpoint}",
            targetAgentId, agent.Endpoints.A2AEndpointUrl);

        // A2A SDK supports task streaming via GetTaskAsync polling  
        // For now, we'll send the message and return single response
        // Full streaming would require SSE/WebSocket implementation
        var response = await client.SendMessageAsync(message, cancellationToken);
        
        _logger.LogDebug(
            "Received initial response from agent {AgentId}",
            targetAgentId);

        // Convert A2AResponse to AgentMessage
        yield return (AgentMessage)(object)response;
    }

    public async Task<bool> CanHandleAsync(string targetAgentId, CancellationToken cancellationToken = default)
    {
        var agent = await _agentRegistry.GetAgentByIdAsync(targetAgentId, cancellationToken);
        return agent != null && 
               !string.IsNullOrEmpty(agent.Endpoints.A2AEndpointUrl) &&
               agent.Communication.SupportsA2ADirect;
    }
}
