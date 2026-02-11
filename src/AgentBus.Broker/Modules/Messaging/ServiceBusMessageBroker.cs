using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using System.Text.Json;

namespace AgentBus.Broker.Modules.Messaging;

public sealed class ServiceBusMessageBroker : IMessageBroker
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ILogger<ServiceBusMessageBroker> _logger;

    public ServiceBusMessageBroker(
        ServiceBusClient client,
        IConfiguration configuration,
        ILogger<ServiceBusMessageBroker> logger)
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

    public async Task<string> SendMessageAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        var queueName = $"agent-{envelope.To}-inbox";
        var sender = _client.CreateSender(queueName);

        var message = new ServiceBusMessage(JsonSerializer.Serialize(envelope))
        {
            MessageId = envelope.MessageId,
            CorrelationId = envelope.CorrelationId,
            TimeToLive = envelope.Ttl ?? TimeSpan.FromHours(24)
        };

        if (envelope.Headers != null)
        {
            foreach (var header in envelope.Headers)
            {
                message.ApplicationProperties[header.Key] = header.Value;
            }
        }

        await sender.SendMessageAsync(message, cancellationToken);
        return envelope.MessageId;
    }

    public async Task<MessageEnvelope?> ReceiveMessageAsync(
        string agentId,
        int maxWaitTimeSeconds,
        CancellationToken cancellationToken)
    {
        var queueName = $"agent-{agentId}-inbox";
        var receiver = _client.CreateReceiver(queueName);

        var message = await receiver.ReceiveMessageAsync(
            TimeSpan.FromSeconds(maxWaitTimeSeconds),
            cancellationToken);

        if (message == null)
        {
            return null;
        }

        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(message.Body.ToString());
        return envelope;
    }

    public async Task AcknowledgeMessageAsync(
        string agentId,
        string messageId,
        CancellationToken cancellationToken)
    {
        var queueName = $"agent-{agentId}-inbox";
        var receiver = _client.CreateReceiver(queueName);
        
        // In a real implementation, we would complete the message by sequence number
        // For now, this is a placeholder
        _logger.LogInformation("Acknowledging message {MessageId} from queue {QueueName}", messageId, queueName);
    }

    public async Task CreateInboxQueueAsync(string agentId, CancellationToken cancellationToken)
    {
        var queueName = $"agent-{agentId}-inbox";
        
        if (!await _adminClient.QueueExistsAsync(queueName, cancellationToken))
        {
            var options = new CreateQueueOptions(queueName)
            {
                MaxDeliveryCount = 10,
                DefaultMessageTimeToLive = TimeSpan.FromDays(1),
                DeadLetteringOnMessageExpiration = true,
                RequiresDuplicateDetection = true,
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10)
            };

            await _adminClient.CreateQueueAsync(options, cancellationToken);
            _logger.LogInformation("Created inbox queue {QueueName}", queueName);
        }
    }

    public async Task DeleteInboxQueueAsync(string agentId, CancellationToken cancellationToken)
    {
        var queueName = $"agent-{agentId}-inbox";
        
        if (await _adminClient.QueueExistsAsync(queueName, cancellationToken))
        {
            await _adminClient.DeleteQueueAsync(queueName, cancellationToken);
            _logger.LogInformation("Deleted inbox queue {QueueName}", queueName);
        }
    }
}
