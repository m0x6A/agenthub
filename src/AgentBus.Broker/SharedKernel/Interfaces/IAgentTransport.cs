using A2A;

namespace AgentBus.Broker.SharedKernel.Interfaces;

/// <summary>
/// Defines transport abstraction for agent-to-agent communication.
/// </summary>
public interface IAgentTransport
{
    /// <summary>
    /// Gets the transport type identifier.
    /// </summary>
    TransportType Type { get; }

    /// <summary>
    /// Sends a message to an agent and awaits a response (non-streaming).
    /// </summary>
    Task<AgentMessage> SendMessageAsync(
        string targetAgentId, 
        MessageSendParams message, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams messages to an agent (HTTP/SSE only).
    /// </summary>
    IAsyncEnumerable<AgentMessage> StreamMessagesAsync(
        string targetAgentId, 
        MessageSendParams message, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this transport can handle the specified agent.
    /// </summary>
    Task<bool> CanHandleAsync(string targetAgentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Supported transport types for agent communication.
/// </summary>
public enum TransportType
{
    /// <summary>
    /// Azure Service Bus queue-based transport (internal agents).
    /// </summary>
    ServiceBus,

    /// <summary>
    /// HTTP/JSON-RPC transport (A2A protocol, external agents).
    /// </summary>
    Http
}
