using AgentBus.Broker.SharedKernel.Models;
using Bogus;

namespace AgentBus.Broker.Tests.Helpers;

public static class TestDataBuilder
{
    private static readonly Faker Faker = new();

    public static Agent CreateTestAgent(
        string? id = null,
        string? name = null,
        string? version = null,
        AgentStatus status = AgentStatus.Active,
        string[]? capabilities = null)
    {
        id ??= $"test-agent-{Faker.Random.AlphaNumeric(8)}";
        
        return new Agent(
            Id: id,
            PartitionKey: id,
            Name: name ?? Faker.Company.CompanyName(),
            Version: version ?? "1.0.0",
            Status: status,
            Capabilities: capabilities ?? new[] { "test-capability", "another-capability" },
            MessageTypes: new MessageTypes(
                Accepts: new[] { "*.test.*" },
                Emits: new[] { "test.event.*" }),
            Identity: new AgentIdentity(
                ManagedIdentityId: $"/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{id}",
                PrincipalId: Faker.Random.Guid().ToString(),
                TenantId: Faker.Random.Guid().ToString()),
            Endpoints: new AgentEndpoints(
                InboxQueueName: $"agent-{id}-inbox",
                HealthCheckUrl: null),
            Metadata: null,
            Timestamps: new AgentTimestamps(
                RegisteredAt: DateTime.UtcNow.AddHours(-1),
                LastHeartbeat: DateTime.UtcNow));
    }

    public static MessageEnvelope CreateTestMessage(
        string? from = null,
        string? to = null,
        string? messageType = null)
    {
        return new MessageEnvelope(
            MessageId: Faker.Random.Guid().ToString(),
            CorrelationId: null,
            ConversationId: null,
            From: from ?? "sender-agent",
            To: to ?? "receiver-agent",
            MessageType: messageType ?? "test.message",
            Timestamp: DateTime.UtcNow,
            Ttl: TimeSpan.FromHours(24),
            Payload: System.Text.Json.JsonSerializer.SerializeToElement(new { test = "data" }),
            ReplyTo: null,
            Headers: null);
    }

    public static EventEnvelope CreateTestEvent(
        string? source = null,
        string? eventType = null)
    {
        return new EventEnvelope(
            EventId: Faker.Random.Guid().ToString(),
            EventType: eventType ?? "events.test.event",
            Source: source ?? "publisher-agent",
            Timestamp: DateTime.UtcNow,
            DataVersion: "1.0.0",
            Data: System.Text.Json.JsonSerializer.SerializeToElement(new { test = "data" }),
            Headers: null);
    }

    public static Subscription CreateTestSubscription(
        string? agentId = null,
        string? eventType = null)
    {
        agentId ??= "subscriber-agent";
        eventType ??= "events.test.event";
        
        return new Subscription(
            Id: Faker.Random.Guid().ToString(),
            PartitionKey: agentId,
            AgentId: agentId,
            EventType: eventType,
            ServiceBusSubscriptionName: $"sub-{agentId}-{Faker.Random.AlphaNumeric(8)}",
            Filters: null,
            CreatedAt: DateTime.UtcNow);
    }
}
