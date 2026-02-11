namespace AgentBus.Broker.SharedKernel.Models;

public sealed record Subscription(
    string Id,
    string PartitionKey,
    string AgentId,
    string EventType,
    string ServiceBusSubscriptionName,
    string[]? Filters,
    DateTime CreatedAt);
