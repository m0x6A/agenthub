using Serilog;
using System.Collections.Concurrent;

namespace AgentBus.Examples.LogisticsUI;

/// <summary>
/// Service that tracks and simulates agent communication for demonstration
/// </summary>
public class AgentCommunicationService
{
    private readonly ConcurrentQueue<AgentMessage> _messageQueue = new();
    private readonly ConcurrentDictionary<string, string> _activeConversations = new();

    public AgentCommunicationService()
    {
    }

    public record AgentMessage(
        string MessageId,
        string From,
        string To,
        string Message,
        DateTime Timestamp,
        string Type); // "SENT", "RECEIVED", "ANALYSIS", "DECISION"

    public void LogMessage(string from, string to, string message, string type = "SENT")
    {
        var msg = new AgentMessage(
            MessageId: Guid.NewGuid().ToString(),
            From: from,
            To: to,
            Message: message,
            Timestamp: DateTime.UtcNow,
            Type: type);

        _messageQueue.Enqueue(msg);
        Log.Information($"📨 {from} → {to} [{type}]: {message}");
    }

    public List<AgentMessage> GetMessages(int count = 100)
    {
        return _messageQueue.TakeLast(count).ToList();
    }

    public IEnumerable<AgentMessage> GetMessagesAsStream()
    {
        var processed = 0;
        while (processed < _messageQueue.Count)
        {
            var messages = _messageQueue.TakeLast(_messageQueue.Count - processed).ToList();
            foreach (var msg in messages)
            {
                yield return msg;
            }
            processed = _messageQueue.Count;
            Thread.Sleep(100);
        }
    }

    public void StartConversation(string conversationId)
    {
        _activeConversations.TryAdd(conversationId, DateTime.UtcNow.ToString());
    }

    public void EndConversation(string conversationId)
    {
        _activeConversations.TryRemove(conversationId, out _);
    }

    public Dictionary<string, string> GetActiveConversations() => new(_activeConversations);
}
