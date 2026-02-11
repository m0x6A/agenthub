using A2A;
using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.SharedKernel.Services;

/// <summary>
/// Service for routing agent-to-agent communication through appropriate transports.
/// </summary>
public sealed class AgentCommunicationService
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IEnumerable<IAgentTransport> _transports;
    private readonly ILogger<AgentCommunicationService> _logger;

    public AgentCommunicationService(
        IAgentRegistry agentRegistry,
        IEnumerable<IAgentTransport> transports,
        ILogger<AgentCommunicationService> logger)
    {
        _agentRegistry = agentRegistry;
        _transports = transports;
        _logger = logger;
    }

    /// <summary>
    /// Sends a message to an agent using the appropriate transport.
    /// </summary>
    public async Task<AgentMessage> SendMessageAsync(
        string targetAgentId,
        MessageSendParams message,
        TransportPreference? preferredTransport = null,
        CancellationToken cancellationToken = default)
    {
        var transport = await SelectTransportAsync(targetAgentId, preferredTransport ?? TransportPreference.Auto, streaming: false, cancellationToken);

        _logger.LogInformation(
            "Routing message to agent {AgentId} via {TransportType} transport",
            targetAgentId, transport.Type);

        return await transport.SendMessageAsync(targetAgentId, message, cancellationToken);
    }

    /// <summary>
    /// Streams messages to an agent using HTTP transport (Service Bus doesn't support streaming).
    /// </summary>
    public async IAsyncEnumerable<AgentMessage> StreamMessagesAsync(
        string targetAgentId,
        MessageSendParams message,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Streaming always requires HTTP transport
        var transport = await SelectTransportAsync(targetAgentId, TransportPreference.Http, streaming: true, cancellationToken);

        _logger.LogInformation(
            "Starting message stream to agent {AgentId} via {TransportType} transport",
            targetAgentId, transport.Type);

        await foreach (var streamedMessage in transport.StreamMessagesAsync(targetAgentId, message, cancellationToken))
        {
            yield return streamedMessage;
        }
    }

    /// <summary>
    /// Selects the appropriate transport based on agent capabilities and preferences.
    /// </summary>
    private async Task<IAgentTransport> SelectTransportAsync(
        string targetAgentId,
        TransportPreference preference,
        bool streaming,
        CancellationToken cancellationToken)
    {
        var agent = await _agentRegistry.GetAgentByIdAsync(targetAgentId, cancellationToken);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent '{targetAgentId}' not found");
        }

        // Streaming always requires HTTP
        if (streaming)
        {
            var httpTransport = _transports.FirstOrDefault(t => t.Type == TransportType.Http);
            if (httpTransport == null || !await httpTransport.CanHandleAsync(targetAgentId, cancellationToken))
            {
                throw new NotSupportedException($"Agent '{targetAgentId}' does not support HTTP transport required for streaming");
            }
            return httpTransport;
        }

        // Select based on preference
        IAgentTransport? selectedTransport = preference switch
        {
            TransportPreference.ServiceBus => _transports.FirstOrDefault(t => t.Type == TransportType.ServiceBus),
            TransportPreference.Http => _transports.FirstOrDefault(t => t.Type == TransportType.Http),
            TransportPreference.Auto => await AutoSelectTransportAsync(targetAgentId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, "Unknown transport preference")
        };

        // Validate selected transport can handle the agent
        if (selectedTransport == null || !await selectedTransport.CanHandleAsync(targetAgentId, cancellationToken))
        {
            // Fallback: try any available transport
            foreach (var transport in _transports)
            {
                if (await transport.CanHandleAsync(targetAgentId, cancellationToken))
                {
                    _logger.LogWarning(
                        "Preferred transport {Preference} not available for agent {AgentId}, falling back to {TransportType}",
                        preference, targetAgentId, transport.Type);
                    return transport;
                }
            }

            throw new InvalidOperationException(
                $"No transport available for agent '{targetAgentId}'. " +
                $"Ensure agent has either InboxQueueName or A2AEndpointUrl configured.");
        }

        return selectedTransport;
    }

    /// <summary>
    /// Automatically selects the best transport for the agent.
    /// Preference order: ServiceBus (internal) > HTTP (external/interop)
    /// </summary>
    private async Task<IAgentTransport?> AutoSelectTransportAsync(string targetAgentId, CancellationToken cancellationToken)
    {
        var agent = await _agentRegistry.GetAgentByIdAsync(targetAgentId, cancellationToken);
        if (agent == null)
        {
            return null;
        }

        // Prefer Service Bus for internal agents (better performance, reliability, cost)
        if (!string.IsNullOrEmpty(agent.Endpoints.InboxQueueName))
        {
            var serviceBusTransport = _transports.FirstOrDefault(t => t.Type == TransportType.ServiceBus);
            if (serviceBusTransport != null && await serviceBusTransport.CanHandleAsync(targetAgentId, cancellationToken))
            {
                _logger.LogDebug("Auto-selected Service Bus transport for internal agent {AgentId}", targetAgentId);
                return serviceBusTransport;
            }
        }

        // Fallback to HTTP for external agents or if Service Bus unavailable
        if (!string.IsNullOrEmpty(agent.Endpoints.A2AEndpointUrl) && agent.Communication.SupportsA2ADirect)
        {
            var httpTransport = _transports.FirstOrDefault(t => t.Type == TransportType.Http);
            if (httpTransport != null && await httpTransport.CanHandleAsync(targetAgentId, cancellationToken))
            {
                _logger.LogDebug("Auto-selected HTTP transport for external agent {AgentId}", targetAgentId);
                return httpTransport;
            }
        }

        return null;
    }
}
