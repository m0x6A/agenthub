using System.ComponentModel;
using Microsoft.SemanticKernel;
using Serilog;
using AgentBus.Examples.Shared;

namespace AgentBus.Examples.LogisticsCoordination.Plugins;

/// <summary>
/// Direct messaging plugin - allows agents to send natural language requests to other agents
/// </summary>
public class DirectMessagingPlugin
{
    private readonly IAgentBusClient _agentBus;
    private readonly string _agentId;
    private readonly ILogger _logger;

    public DirectMessagingPlugin(IAgentBusClient agentBus, string agentId, ILogger logger)
    {
        _agentBus = agentBus;
        _agentId = agentId;
        _logger = logger;
    }

    [KernelFunction("send_message_to_agent")]
    [Description("Send a natural language message to another agent asking for help, advice, or information. Wait for their response before proceeding.")]
    public async Task<string> SendMessageToAgentAsync(
        [Description("ID of the agent to send message to (e.g., 'warehouse-lead-agent', 'shipping-agent')")] string recipientAgentId,
        [Description("Your natural language message/question for the other agent")] string message)
    {
        try
        {
            _logger.Information("💬 {SenderAgent} sending message to {RecipientAgent}", _agentId, recipientAgentId);
            _logger.Information("📤 Message: {Message}", message);

            // In a real implementation, this would send via Service Bus direct message queue
            // For now, we'll return a simulated response that would come from the other agent
            
            // This is where the agent-to-agent communication happens
            // The message goes to the recipient's inbox queue
            await _agentBus.PublishEventAsync("agent.message.sent", new
            {
                from = _agentId,
                to = recipientAgentId,
                message = message,
                timestamp = DateTime.UtcNow
            });

            return $"Message sent to {recipientAgentId}. Awaiting response...";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to send message to {Agent}", recipientAgentId);
            return $"Failed to send message: {ex.Message}";
        }
    }

    [KernelFunction("log_analysis")]
    [Description("Log your internal analysis/reasoning about the situation (for transparency)")]
    public Task<string> LogAnalysisAsync(
        [Description("What you're analyzing")] string subject,
        [Description("Your reasoning and findings")] string analysis)
    {
        _logger.Information("🧠 {Agent} analyzing {Subject}", _agentId, subject);
        _logger.Information("   Reasoning: {Analysis}", analysis);
        return Task.FromResult($"Logged analysis about {subject}");
    }
}
