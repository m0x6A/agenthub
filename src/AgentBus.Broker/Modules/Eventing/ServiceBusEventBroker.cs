using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using System.Text.Json;

namespace AgentBus.Broker.Modules.Eventing;

public sealed class ServiceBusEventBroker : IEventBroker
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ILogger<ServiceBusEventBroker> _logger;
    private readonly Dictionary<string, Subscription> _subscriptions = new();

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
        
        if (!await _adminClient.TopicExistsAsync(topicName, cancellationToken))
        {
            var options = new CreateTopicOptions(topicName)
            {
                DefaultMessageTimeToLive = TimeSpan.FromDays(1),
                RequiresDuplicateDetection = true,
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10)
            };
            await _adminClient.CreateTopicAsync(options, cancellationToken);
        }

        var sender = _client.CreateSender(topicName);
        var message = new ServiceBusMessage(JsonSerializer.Serialize(envelope))
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

        await sender.SendMessageAsync(message, cancellationToken);
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

        if (!await _adminClient.TopicExistsAsync(topicName, cancellationToken))
        {
            var topicOptions = new CreateTopicOptions(topicName);
            await _adminClient.CreateTopicAsync(topicOptions, cancellationToken);
        }

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

        var topicName = subscription.EventType.Replace(".", "-");
        var receiver = _client.CreateReceiver(topicName, subscription.ServiceBusSubscriptionName);

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

        var topicName = subscription.EventType.Replace(".", "-");
        
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
}
