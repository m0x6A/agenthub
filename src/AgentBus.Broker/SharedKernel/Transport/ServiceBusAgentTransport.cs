using A2A;
using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using Azure.Messaging.ServiceBus;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AgentBus.Broker.SharedKernel.Transport;

/// <summary>
/// Service Bus implementation of agent transport using request-reply pattern.
/// </summary>
public sealed class ServiceBusAgentTransport : IAgentTransport
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<ServiceBusAgentTransport> _logger;
    private readonly string _replyQueueName;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<AgentMessage>> _pendingRequests = new();
    private ServiceBusProcessor? _replyProcessor;

    public TransportType Type => TransportType.ServiceBus;

    public ServiceBusAgentTransport(
        ServiceBusClient serviceBusClient,
        IAgentRegistry agentRegistry,
        IConfiguration configuration,
        ILogger<ServiceBusAgentTransport> logger)
    {
        _serviceBusClient = serviceBusClient;
        _agentRegistry = agentRegistry;
        _logger = logger;
        _replyQueueName = configuration["ServiceBus:ReplyQueueName"] ?? "agent-replies";

        InitializeReplyProcessorAsync().GetAwaiter().GetResult();
    }

    public async Task<AgentMessage> SendMessageAsync(
        string targetAgentId,
        MessageSendParams message,
        CancellationToken cancellationToken = default)
    {
        var agent = await _agentRegistry.GetAgentByIdAsync(targetAgentId, cancellationToken);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent '{targetAgentId}' not found");
        }

        if (string.IsNullOrEmpty(agent.Endpoints.InboxQueueName))
        {
            throw new InvalidOperationException($"Agent '{targetAgentId}' does not have an inbox queue configured");
        }

        var correlationId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<AgentMessage>();
        _pendingRequests[correlationId] = tcs;

        try
        {
            var sender = _serviceBusClient.CreateSender(agent.Endpoints.InboxQueueName);
            await using var _ = sender.ConfigureAwait(false);

            var messageBody = JsonSerializer.Serialize(message);
            var serviceBusMessage = new ServiceBusMessage(messageBody)
            {
                CorrelationId = correlationId,
                ReplyTo = _replyQueueName,
                ContentType = "application/json",
                Subject = "agent-message",
                MessageId = message.Message.MessageId ?? Guid.NewGuid().ToString(),
                TimeToLive = TimeSpan.FromMinutes(5)
            };

            // Add context information
            serviceBusMessage.ApplicationProperties["ContextId"] = message.Message.ContextId;
            serviceBusMessage.ApplicationProperties["MessageRole"] = message.Message.Role.ToString();

            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);

            _logger.LogInformation(
                "Sent message to agent {AgentId} via Service Bus, CorrelationId: {CorrelationId}",
                targetAgentId, correlationId);

            // Wait for response with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            var responseTask = tcs.Task;
            var completedTask = await Task.WhenAny(responseTask, Task.Delay(Timeout.Infinite, timeoutCts.Token));

            if (completedTask != responseTask)
            {
                throw new TimeoutException($"No response received from agent '{targetAgentId}' within timeout period");
            }

            return await responseTask;
        }
        finally
        {
            _pendingRequests.TryRemove(correlationId, out _);
        }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async IAsyncEnumerable<AgentMessage> StreamMessagesAsync(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        string targetAgentId,
        MessageSendParams message,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Streaming is not supported over Service Bus transport. Use HTTP transport for streaming scenarios.");
#pragma warning disable CS0162 // Unreachable code detected
        yield break;
#pragma warning restore CS0162 // Unreachable code detected
    }

    public async Task<bool> CanHandleAsync(string targetAgentId, CancellationToken cancellationToken = default)
    {
        var agent = await _agentRegistry.GetAgentByIdAsync(targetAgentId, cancellationToken);
        return agent != null && !string.IsNullOrEmpty(agent.Endpoints.InboxQueueName);
    }

    private async Task InitializeReplyProcessorAsync()
    {
        // Note: Reply queue should be created during module initialization
        // Create processor for reply messages
        _replyProcessor = _serviceBusClient.CreateProcessor(_replyQueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 10
        });

        _replyProcessor.ProcessMessageAsync += ProcessReplyMessageAsync;
        _replyProcessor.ProcessErrorAsync += ProcessErrorAsync;

        await _replyProcessor.StartProcessingAsync();
        _logger.LogInformation("Started reply processor on queue: {QueueName}", _replyQueueName);
    }

    private Task ProcessReplyMessageAsync(ProcessMessageEventArgs args)
    {
        var correlationId = args.Message.CorrelationId;

        if (string.IsNullOrEmpty(correlationId))
        {
            _logger.LogWarning("Received reply message without correlation ID");
            return args.CompleteMessageAsync(args.Message);
        }

        if (_pendingRequests.TryGetValue(correlationId, out var tcs))
        {
            try
            {
                var messageBody = args.Message.Body.ToString();
                var response = JsonSerializer.Deserialize<AgentMessage>(messageBody);

                if (response != null)
                {
                    tcs.TrySetResult(response);
                    _logger.LogInformation(
                        "Received reply for CorrelationId: {CorrelationId}",
                        correlationId);
                }
                else
                {
                    tcs.TrySetException(new InvalidOperationException("Failed to deserialize response message"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing reply message for CorrelationId: {CorrelationId}", correlationId);
                tcs.TrySetException(ex);
            }
        }
        else
        {
            _logger.LogWarning("Received reply for unknown CorrelationId: {CorrelationId}", correlationId);
        }

        return args.CompleteMessageAsync(args.Message);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Error processing Service Bus message. Source: {ErrorSource}, Entity: {EntityPath}",
            args.ErrorSource,
            args.EntityPath);
        return Task.CompletedTask;
    }
}
