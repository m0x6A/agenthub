using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace AgentBus.Examples.Shared.Plugins;

/// <summary>
/// Semantic Kernel plugin that allows agents to publish events to AgentBus.
/// The agent autonomously decides WHEN and WHAT to publish based on its reasoning.
/// </summary>
public class AgentBusPlugin
{
    private readonly IAgentBusClient _agentBus;
    private readonly string _agentId;

    public AgentBusPlugin(IAgentBusClient agentBus, string agentId)
    {
        _agentBus = agentBus;
        _agentId = agentId;
    }

    [KernelFunction("publish_event")]
    [Description("Publishes an event to the AgentBus so other agents can react to it. Use this to communicate significant findings, decisions, or state changes to other agents.")]
    public async Task<string> PublishEventAsync(
        [Description("Event type using dot notation (e.g., 'customer.inquiry.analyzed', 'inventory.check.completed')")] string eventType,
        [Description("Event payload as JSON string with all relevant data")] string payloadJson)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);
            await _agentBus.PublishEventAsync(eventType, payload!);
            return $"Successfully published {eventType} event with payload to AgentBus";
        }
        catch (Exception ex)
        {
            return $"Failed to publish event: {ex.Message}";
        }
    }

    [KernelFunction("get_agent_context")]
    [Description("Gets information about this agent's identity and capabilities. Use this for self-reflection or when including context in events.")]
    public Task<string> GetAgentContextAsync()
    {
        return Task.FromResult($"Agent ID: {_agentId}, Transport: Connected to AgentBus");
    }
}
