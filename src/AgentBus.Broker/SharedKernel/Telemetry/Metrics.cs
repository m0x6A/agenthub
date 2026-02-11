using System.Diagnostics.Metrics;

namespace AgentBus.Broker.SharedKernel.Telemetry;

public sealed class AgentBusMetrics
{
    private readonly Counter<long> _agentsRegisteredTotal;
    private readonly Counter<long> _messagesPerSecond;
    private readonly Counter<long> _eventsPerSecond;
    private readonly ObservableGauge<int> _dlqDepth;

    public AgentBusMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("AgentBus.Broker");

        _agentsRegisteredTotal = meter.CreateCounter<long>(
            "agents_registered_total",
            description: "Total number of agents registered");

        _messagesPerSecond = meter.CreateCounter<long>(
            "messages_per_second",
            description: "Rate of messages sent per second");

        _eventsPerSecond = meter.CreateCounter<long>(
            "events_per_second",
            description: "Rate of events published per second");

        _dlqDepth = meter.CreateObservableGauge(
            "dlq_depth",
            observeValue: () => 0, // Will be implemented with actual DLQ monitoring
            description: "Number of messages in dead-letter queues");
    }

    public void IncrementAgentsRegistered() => _agentsRegisteredTotal.Add(1);
    public void IncrementMessagesSent() => _messagesPerSecond.Add(1);
    public void IncrementEventsPublished() => _eventsPerSecond.Add(1);
}
