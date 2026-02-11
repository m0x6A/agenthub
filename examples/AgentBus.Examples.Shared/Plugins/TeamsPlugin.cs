using System.ComponentModel;
using AgentBus.Examples.Shared.ExternalSystems;
using Microsoft.SemanticKernel;

namespace AgentBus.Examples.Shared.Plugins;

/// <summary>
/// Semantic Kernel plugin for Microsoft Teams integration.
/// Agent autonomously decides when to notify teams and what information to include.
/// </summary>
public class TeamsPlugin
{
    private readonly MockTeamsApi _teamsApi;

    public TeamsPlugin(MockTeamsApi teamsApi)
    {
        _teamsApi = teamsApi;
    }

    [KernelFunction("post_to_channel")]
    [Description("Posts a message to a Teams channel. Use this to notify teams about important events or issues.")]
    public async Task<string> PostToChannelAsync(
        [Description("The channel to post to (e.g., 'Operations', 'Customer-Service', 'Finance')")] string channel,
        [Description("Message title")] string title,
        [Description("The message body")] string message,
        [Description("Priority level (normal, high, urgent)")] string priority = "normal")
    {
        await _teamsApi.PostToChannelAsync(channel, title, message, priority);
        return $"Posted message to #{channel} channel";
    }

    [KernelFunction("post_adaptive_card")]
    [Description("Posts a rich, interactive Adaptive Card to Teams. Use this for structured notifications that require attention or action.")]
    public async Task<string> PostAdaptiveCardAsync(
        [Description("The channel to post to")] string channel,
        [Description("Card title")] string title,
        [Description("Field key-value pairs as JSON (e.g., '{\"Order ID\":\"ORD-123\",\"Amount\":\"$500\"}')")] string fieldsJson)
    {
        var fields = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(fieldsJson) ?? new();
        await _teamsApi.PostAdaptiveCardAsync(channel, title, fields);
        return $"Posted adaptive card '{title}' to #{channel}";
    }
}
