using Serilog;

namespace AgentBus.Examples.Shared.ExternalSystems;

/// <summary>
/// Mock Microsoft 365 Teams API for posting messages to channels
/// </summary>
public class MockTeamsApi
{
    private readonly Dictionary<string, List<TeamsMessage>> _channelMessages = new();

    public async Task PostToChannelAsync(string channel, string title, string message, string priority = "normal", string? mention = null)
    {
        await Task.Delay(80); // Simulate API latency

        if (!_channelMessages.ContainsKey(channel))
            _channelMessages[channel] = new List<TeamsMessage>();

        var teamsMessage = new TeamsMessage
        {
            Channel = channel,
            Title = title,
            Message = message,
            Priority = priority,
            Mention = mention,
            Timestamp = DateTime.UtcNow
        };

        _channelMessages[channel].Add(teamsMessage);

        var priorityEmoji = priority switch
        {
            "urgent" => "🚨",
            "high" => "⚠️",
            "normal" => "ℹ️",
            _ => "📝"
        };

        Console.WriteLine($"\n{new string('═', 80)}");
        Console.WriteLine($"📱 MICROSOFT TEAMS - #{channel}");
        Console.WriteLine(new string('═', 80));
        Console.WriteLine($"{priorityEmoji} {title}");
        if (mention != null)
            Console.WriteLine($"👤 @{mention}");
        Console.WriteLine($"\n{message}");
        Console.WriteLine($"\n🕐 {teamsMessage.Timestamp:HH:mm:ss}");
        Console.WriteLine(new string('═', 80) + "\n");

        Log.Information("[Teams API] Posted to #{Channel}: {Title}", channel, title);
    }

    public async Task PostAdaptiveCardAsync(string channel, string title, Dictionary<string, string> fields, string? actionUrl = null)
    {
        await Task.Delay(100);

        var message = $"**{title}**\n\n";
        foreach (var field in fields)
        {
            message += $"**{field.Key}**: {field.Value}\n";
        }
        if (actionUrl != null)
        {
            message += $"\n[View Details]({actionUrl})";
        }

        await PostToChannelAsync(channel, title, message, "normal");
    }

    public List<TeamsMessage> GetChannelMessages(string channel)
    {
        return _channelMessages.GetValueOrDefault(channel, new List<TeamsMessage>());
    }
}

public record TeamsMessage
{
    public required string Channel { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string Priority { get; init; }
    public string? Mention { get; init; }
    public required DateTime Timestamp { get; init; }
}
