namespace AgentBus.Broker.SharedKernel.Models;

/// <summary>
/// Agent's preferred transport mechanism for receiving messages.
/// </summary>
public enum TransportPreference
{
    /// <summary>
    /// Prefer Azure Service Bus queue-based communication (internal agents).
    /// Lower latency, better reliability, no public endpoint required.
    /// </summary>
    ServiceBus,

    /// <summary>
    /// Prefer HTTP/JSON-RPC via A2A protocol (external agents, cross-platform).
    /// Required for agents outside Azure, supports streaming.
    /// </summary>
    Http,

    /// <summary>
    /// Auto-select based on agent capabilities and endpoints.
    /// ServiceBus if inbox queue exists, HTTP if A2A endpoint exists.
    /// </summary>
    Auto
}
