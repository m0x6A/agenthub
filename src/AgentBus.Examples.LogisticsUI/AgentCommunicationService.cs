using Serilog;
using System.Collections.Concurrent;

namespace AgentBus.Examples.LogisticsUI;

/// <summary>
/// Service that stores and provides access to real agent events for the UI
/// </summary>
public class AgentCommunicationService
{
    private readonly ConcurrentQueue<AgentEvent> _eventQueue = new();
    private const int MaxEvents = 500; // Keep last 500 events

    public record AgentEvent(
        string EventId,
        string EventType,
        string Source,
        string Data,
        DateTime Timestamp);

    public void LogEvent(string eventType, string source, string data, DateTime timestamp)
    {
        var evt = new AgentEvent(
            EventId: Guid.NewGuid().ToString(),
            EventType: eventType,
            Source: source,
            Data: data,
            Timestamp: timestamp);

        _eventQueue.Enqueue(evt);
        
        // Keep only last MaxEvents
        while (_eventQueue.Count > MaxEvents)
        {
            _eventQueue.TryDequeue(out _);
        }

        Log.Information("📨 Event stored: {EventType} from {Source}", eventType, source);
    }

    public List<AgentEvent> GetEvents(int count = 100)
    {
        return _eventQueue.TakeLast(count).ToList();
    }

    public int GetEventCount() => _eventQueue.Count;
}
