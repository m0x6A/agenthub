using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using System.Text.Json;

namespace AgentBus.Broker.Modules.Eventing;

public sealed class ServiceBusEventBroker : IEventBroker
{
    private const string GlobalBroadcastTopic = "agent-events-broadcast";
    
    private readonly ServiceBusClient _client;
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ILogger<ServiceBusEventBroker> _logger;
    private readonly Dictionary<string, Subscription> _subscriptions = new();
    private readonly HashSet<string> _createdTopics = new(); // Cache created topics
    private readonly SemaphoreSlim _topicCreationLock = new(1, 1); // Thread-safe topic creation

    public ServiceBusEventBroker(
        ServiceBusClient client,
        IConfiguration configuration,
        ILogger<ServiceBusEventBroker> logger)
    {
        _client = client;
        _logger = logger;
        
        var connectionString = configuration["ServiceBus:ConnectionString"];
        if (!string.IsNullOrEmpty(connectionString))
        {
            _adminClient = new ServiceBusAdministrationClient(connectionString);
        }
        else
        {
            throw new InvalidOperationException("ServiceBus:ConnectionString is required");
        }
    }

    public async Task<string> PublishEventAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var topicName = envelope.EventType.Replace(".", "-");
        
        // Ensure topics exist (cached - only creates once)
        await EnsureTopicExistsAsync(topicName, cancellationToken);
        await EnsureTopicExistsAsync(GlobalBroadcastTopic, cancellationToken);

        var messageBody = JsonSerializer.Serialize(envelope);
        var message = new ServiceBusMessage(messageBody)
        {
            MessageId = envelope.EventId,
            Subject = envelope.EventType
        };

        if (envelope.Headers != null)
        {
            foreach (var header in envelope.Headers)
            {
                message.ApplicationProperties[header.Key] = header.Value;
            }
        }

        // Publish to specific event type topic
        var specificSender = _client.CreateSender(topicName);
        await using var _ = specificSender.ConfigureAwait(false);
        await specificSender.SendMessageAsync(message, cancellationToken);
        
        // Also publish to global broadcast topic so all agents receive it
        var globalMessage = new ServiceBusMessage(messageBody)
        {
            MessageId = $"{envelope.EventId}-broadcast",
            Subject = envelope.EventType
        };
        
        if (envelope.Headers != null)
        {
            foreach (var header in envelope.Headers)
            {
                globalMessage.ApplicationProperties[header.Key] = header.Value;
            }
        }
        
        var globalSender = _client.CreateSender(GlobalBroadcastTopic);
        await using var __ = globalSender.ConfigureAwait(false);
        await globalSender.SendMessageAsync(globalMessage, cancellationToken);
        
        _logger.LogInformation(
            "Published event {EventId} of type {EventType} to both specific and global broadcast topics",
            envelope.EventId, envelope.EventType);
        
        return envelope.EventId;
    }

    public async Task<Subscription> SubscribeAsync(
        string agentId,
        string eventType,
        string[]? filters,
        CancellationToken cancellationToken)
    {
        var topicName = eventType.Replace(".", "-");
        var subscriptionId = Guid.NewGuid().ToString();
        var subscriptionName = $"sub-{agentId}-{subscriptionId[..8]}";

        // Ensure topic exists (cached)
        await EnsureTopicExistsAsync(topicName, cancellationToken);

        var options = new CreateSubscriptionOptions(topicName, subscriptionName)
        {
            MaxDeliveryCount = 10,
            DeadLetteringOnMessageExpiration = true
        };

        await _adminClient.CreateSubscriptionAsync(options, cancellationToken);

        var subscription = new Subscription(
            Id: subscriptionId,
            PartitionKey: agentId,
            AgentId: agentId,
            EventType: eventType,
            ServiceBusSubscriptionName: subscriptionName,
            Filters: filters,
            CreatedAt: DateTime.UtcNow);

        _subscriptions[subscriptionId] = subscription;
        
        return subscription;
    }

    public async Task<EventEnvelope?> ReceiveEventAsync(
        string subscriptionId,
        int maxWaitTimeSeconds,
        CancellationToken cancellationToken)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out var subscription))
        {
            throw new InvalidOperationException($"Subscription {subscriptionId} not found");
        }

        // Handle global broadcast subscription (EventType = "*")
        var topicName = subscription.EventType == "*" 
            ? GlobalBroadcastTopic 
            : subscription.EventType.Replace(".", "-");
            
        var receiver = _client.CreateReceiver(topicName, subscription.ServiceBusSubscriptionName);
        await using var _ = receiver.ConfigureAwait(false);

        var message = await receiver.ReceiveMessageAsync(
            TimeSpan.FromSeconds(maxWaitTimeSeconds),
            cancellationToken);

        if (message == null)
        {
            return null;
        }

        var envelope = JsonSerializer.Deserialize<EventEnvelope>(message.Body.ToString());
        await receiver.CompleteMessageAsync(message, cancellationToken);
        return envelope;
    }

    public async Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out var subscription))
        {
            throw new InvalidOperationException($"Subscription {subscriptionId} not found");
        }

        // Handle global broadcast subscription (EventType = "*")
        var topicName = subscription.EventType == "*" 
            ? GlobalBroadcastTopic 
            : subscription.EventType.Replace(".", "-");
        
        if (await _adminClient.SubscriptionExistsAsync(topicName, subscription.ServiceBusSubscriptionName, cancellationToken))
        {
            await _adminClient.DeleteSubscriptionAsync(topicName, subscription.ServiceBusSubscriptionName, cancellationToken);
        }

        _subscriptions.Remove(subscriptionId);
        _logger.LogInformation("Unsubscribed from {SubscriptionId}", subscriptionId);
    }

    public async Task DeleteAllSubscriptionsAsync(string agentId, CancellationToken cancellationToken)
    {
        var subscriptionsToDelete = _subscriptions.Values
            .Where(s => s.AgentId == agentId)
            .ToList();

        foreach (var subscription in subscriptionsToDelete)
        {
            await UnsubscribeAsync(subscription.Id, cancellationToken);
        }
        
        _logger.LogInformation("Deleted all subscriptions for agent {AgentId}", agentId);
    }
    
    public async Task<Subscription> SubscribeToAllEventsAsync(string agentId, CancellationToken cancellationToken)
    {
        await EnsureTopicExistsAsync(GlobalBroadcastTopic, cancellationToken);
        
        var subscriptionId = Guid.NewGuid().ToString();
        var subscriptionName = $"sub-{agentId}-global-{subscriptionId[..8]}";

        var options = new CreateSubscriptionOptions(GlobalBroadcastTopic, subscriptionName)
        {
            MaxDeliveryCount = 10,
            DeadLetteringOnMessageExpiration = true,
            DefaultMessageTimeToLive = TimeSpan.FromDays(1)
        };

        await _adminClient.CreateSubscriptionAsync(options, cancellationToken);

        var subscription = new Subscription(
            Id: subscriptionId,
            PartitionKey: agentId,
            AgentId: agentId,
            EventType: "*", // Special marker for global broadcast subscription
            ServiceBusSubscriptionName: subscriptionName,
            Filters: null,
            CreatedAt: DateTime.UtcNow);

        _subscriptions[subscriptionId] = subscription;
        
        _logger.LogInformation(
            "Agent {AgentId} subscribed to global broadcast with subscription {SubscriptionId}",
            agentId, subscriptionId);
        
        return subscription;
    }
    
    /// <summary>
    /// Ensures a topic exists, using cache to avoid repeated admin API calls
    /// </summary>
    private async Task EnsureTopicExistsAsync(string topicName, CancellationToken cancellationToken)
    {
        // Fast path: topic already created
        if (_createdTopics.Contains(topicName))
        {
            return;
        }

        // Slow path: need to check/create topic
        await _topicCreationLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_createdTopics.Contains(topicName))
            {
                return;
            }

            if (!await _adminClient.TopicExistsAsync(topicName, cancellationToken))
            {
                var options = new CreateTopicOptions(topicName)
                {
                    DefaultMessageTimeToLive = TimeSpan.FromDays(1),
                    RequiresDuplicateDetection = true,
                    DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10)
                };
                await _adminClient.CreateTopicAsync(options, cancellationToken);
                _logger.LogInformation("Created Service Bus topic: {TopicName}", topicName);
            }

            _createdTopics.Add(topicName);
        }
        finally
        {
            _topicCreationLock.Release();
        }
    }
}
