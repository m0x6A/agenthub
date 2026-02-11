using System.ComponentModel;
using Microsoft.SemanticKernel;
using Serilog;
using AgentBus.Examples.Shared;

namespace AgentBus.Examples.LogisticsCoordination.Plugins;

/// <summary>
/// Direct messaging plugin - allows agents to send natural language requests to other agents
/// and receive real responses through the AgentBus direct messaging infrastructure
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
    [Description("Send a natural language message to another agent asking for help, advice, or information. Waits for and returns their response.")]
    public async Task<string> SendMessageToAgentAsync(
        [Description("ID of the agent to send message to (e.g., 'warehouse-lead-agent', 'shipping-agent')")] string recipientAgentId,
        [Description("Your natural language message/question for the other agent")] string message)
    {
        try
        {
            // First, verify the agent exists
            var recipientAgent = await _agentBus.GetAgentAsync(recipientAgentId);
            if (recipientAgent == null)
            {
                var errorMsg = $"❌ Agent {recipientAgentId} not found in registry";
                _logger.Error(errorMsg);
                return errorMsg;
            }

            _logger.Information("💬 {SenderAgent} sending message to {RecipientAgent}", _agentId, recipientAgentId);
            _logger.Information("📤 Message: {Message}", message);

            // Send the direct message to their inbox queue
            await _agentBus.SendDirectMessageAsync(recipientAgentId, message);
            _logger.Information("✓ Message sent successfully");

            // Wait for response with 45 second timeout
            _logger.Information("⏳ Waiting for response from {Agent}...", recipientAgentId);
            
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var response = await _agentBus.ReceiveDirectMessageAsync(maxWaitSeconds: 45, cts.Token);

            if (response == null)
            {
                var timeoutMsg = $"⏱️ No response from {recipientAgentId} within 45 seconds";
                _logger.Warning(timeoutMsg);
                return timeoutMsg;
            }

            _logger.Information("📥 Response received from {Agent}:", recipientAgentId);
            _logger.Information("   {Response}", response.Message);
            
            return response.Message;
        }
        catch (OperationCanceledException)
        {
            var timeoutMsg = $"⏱️ Timeout waiting for response from {recipientAgentId}";
            _logger.Warning(timeoutMsg);
            return timeoutMsg;
        }
        catch (Exception ex)
        {
            var errorMsg = $"❌ Failed to send message to {recipientAgentId}: {ex.Message}";
            _logger.Error(ex, errorMsg);
            return errorMsg;
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

