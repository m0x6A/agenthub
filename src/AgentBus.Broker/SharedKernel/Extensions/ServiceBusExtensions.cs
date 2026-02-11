using A2A;
using Azure.Messaging.ServiceBus;
using System.Text.Json;

namespace AgentBus.Broker.SharedKernel.Extensions;

/// <summary>
/// Extension methods for Service Bus operations with A2A types.
/// </summary>
public static class ServiceBusExtensions
{
    /// <summary>
    /// Converts an A2A AgentMessage to a Service Bus message.
    /// </summary>
    public static ServiceBusMessage ToServiceBusMessage(this AgentMessage agentMessage)
    {
        ArgumentNullException.ThrowIfNull(agentMessage);

        var json = JsonSerializer.Serialize(agentMessage, A2AJsonUtilities.DefaultOptions);
        var message = new ServiceBusMessage(json)
        {
            MessageId = agentMessage.MessageId,
            ContentType = "application/json",
            Subject = agentMessage.Role.ToString().ToLowerInvariant()
        };

        // Add correlation tracking
        if (!string.IsNullOrEmpty(agentMessage.TaskId))
        {
            message.CorrelationId = agentMessage.TaskId;
        }

        if (!string.IsNullOrEmpty(agentMessage.ContextId))
        {
            message.SessionId = agentMessage.ContextId;
        }

        // Add metadata as application properties
        if (agentMessage.Metadata != null)
        {
            foreach (var (key, value) in agentMessage.Metadata)
            {
                message.ApplicationProperties[$"metadata.{key}"] = value.ToString();
            }
        }

        // Add extensions
        if (agentMessage.Extensions != null && agentMessage.Extensions.Count > 0)
        {
            message.ApplicationProperties["extensions"] = JsonSerializer.Serialize(agentMessage.Extensions);
        }

        return message;
    }

    /// <summary>
    /// Converts a Service Bus message to an A2A AgentMessage.
    /// </summary>
    public static AgentMessage ToAgentMessage(this ServiceBusReceivedMessage receivedMessage)
    {
        ArgumentNullException.ThrowIfNull(receivedMessage);

        var json = receivedMessage.Body.ToString();
        var agentMessage = JsonSerializer.Deserialize<AgentMessage>(json, A2AJsonUtilities.DefaultOptions)
            ?? throw new InvalidOperationException("Failed to deserialize agent message");

        return agentMessage;
    }

    /// <summary>
    /// Sends an A2A AgentMessage to a Service Bus queue.
    /// </summary>
    public static async Task<string> SendAgentMessageAsync(
        this ServiceBusSender sender,
        AgentMessage agentMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(agentMessage);

        var message = agentMessage.ToServiceBusMessage();
        await sender.SendMessageAsync(message, cancellationToken);
        return agentMessage.MessageId;
    }

    /// <summary>
    /// Receives an A2A AgentMessage from a Service Bus queue.
    /// </summary>
    public static async Task<AgentMessage?> ReceiveAgentMessageAsync(
        this ServiceBusReceiver receiver,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receiver);

        var receivedMessage = await receiver.ReceiveMessageAsync(
            maxWaitTime ?? TimeSpan.FromSeconds(30),
            cancellationToken);

        return receivedMessage?.ToAgentMessage();
    }

    /// <summary>
    /// Publishes an A2A AgentMessage to a Service Bus topic.
    /// </summary>
    public static async Task<string> PublishAgentMessageAsync(
        this ServiceBusSender sender,
        AgentMessage agentMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(agentMessage);

        var message = agentMessage.ToServiceBusMessage();
        
        // For events/topics, add the event type as a property for filtering
        if (agentMessage.Metadata != null && agentMessage.Metadata.TryGetValue("eventType", out var eventType))
        {
            message.ApplicationProperties["eventType"] = eventType.ToString();
        }

        await sender.SendMessageAsync(message, cancellationToken);
        return agentMessage.MessageId;
    }

    /// <summary>
    /// Creates a Service Bus subscription with SQL filter for event types.
    /// </summary>
    public static string CreateSqlFilterForEventType(string eventType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return $"eventType = '{eventType}'";
    }

    /// <summary>
    /// Converts an A2A AgentTask to a Service Bus message for task updates.
    /// </summary>
    public static ServiceBusMessage ToServiceBusMessage(this AgentTask agentTask)
    {
        ArgumentNullException.ThrowIfNull(agentTask);

        var json = JsonSerializer.Serialize(agentTask, A2AJsonUtilities.DefaultOptions);
        var message = new ServiceBusMessage(json)
        {
            MessageId = agentTask.Id,
            ContentType = "application/json",
            Subject = $"task.{agentTask.Status.State.ToString().ToLowerInvariant()}",
            CorrelationId = agentTask.Id,
            SessionId = agentTask.ContextId
        };

        // Add task status as application property for filtering
        message.ApplicationProperties["taskId"] = agentTask.Id;
        message.ApplicationProperties["taskState"] = agentTask.Status.State.ToString();

        // Add metadata
        if (agentTask.Metadata != null)
        {
            foreach (var (key, value) in agentTask.Metadata)
            {
                message.ApplicationProperties[$"metadata.{key}"] = value.ToString();
            }
        }

        return message;
    }

    /// <summary>
    /// Converts a Service Bus message to an A2A AgentTask.
    /// </summary>
    public static AgentTask ToAgentTask(this ServiceBusReceivedMessage receivedMessage)
    {
        ArgumentNullException.ThrowIfNull(receivedMessage);

        var json = receivedMessage.Body.ToString();
        var agentTask = JsonSerializer.Deserialize<AgentTask>(json, A2AJsonUtilities.DefaultOptions)
            ?? throw new InvalidOperationException("Failed to deserialize agent task");

        return agentTask;
    }
}
