using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Azure.Messaging.ServiceBus;
using Serilog;

namespace AgentBus.Examples.Shared;

/// <summary>
/// Service Bus-based AgentBus client for direct Azure Service Bus communication
/// </summary>
public class ServiceBusAgentBusClient : IAgentBusClient
{
    private readonly string _agentBusHttpUrl;
    private readonly HttpClient _httpClient;
    private readonly string _serviceBusConnectionString;
    private readonly string _agentId;
    private ServiceBusClient? _serviceBusClient;
    private ServiceBusReceiver? _receiver;
    private readonly JsonSerializerOptions _jsonOptions;

    public ServiceBusAgentBusClient(string agentBusHttpUrl, string serviceBusConnectionString, string agentId)
    {
        _agentBusHttpUrl = agentBusHttpUrl;
        _httpClient = new HttpClient 
        { 
            BaseAddress = new Uri(agentBusHttpUrl),
            Timeout = TimeSpan.FromSeconds(30) // Set reasonable timeout
        };
        _serviceBusConnectionString = serviceBusConnectionString;
        _agentId = agentId;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task RegisterAgentAsync(AgentRegistration registration)
    {
        // Registration still goes through HTTP API
        const int maxRetries = 10;
        var retryDelay = TimeSpan.FromSeconds(2);
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(registration, _jsonOptions),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync("/api/v1/agents/register", content);
                response.EnsureSuccessStatusCode();
                
                Log.Information("[ServiceBus] Agent registered: {AgentId}", registration.Id);
                return; // Success!
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                Log.Warning(ex, 
                    "Failed to register agent (attempt {Attempt}/{MaxRetries}). Broker may still be starting. Retrying in {Delay}s...",
                    attempt, maxRetries, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay);
                retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 1.5); // Exponential backoff
            }
        }
        
        // If we get here, all retries failed
        throw new InvalidOperationException($"Failed to register agent after {maxRetries} attempts. Is the broker running?");
    }

    public async Task<Subscription> SubscribeToAllEventsAsync()
    {
        // Get subscription details via HTTP
        var response = await _httpClient.PostAsync(
            $"/api/v1/events/subscribe/all?agentId={_agentId}", 
            null);
        response.EnsureSuccessStatusCode();
        
        var subscription = await response.Content.ReadFromJsonAsync<Subscription>(_jsonOptions);
        if (subscription == null)
            throw new InvalidOperationException("Failed to deserialize subscription");

        // Initialize Service Bus receiver
        _serviceBusClient = new ServiceBusClient(_serviceBusConnectionString);
        _receiver = _serviceBusClient.CreateReceiver(subscription.TopicName, subscription.SubscriptionId);
        
        Log.Information("[ServiceBus] Subscribed to global events: {SubscriptionId}", subscription.SubscriptionId);
        return subscription;
    }

    public async Task<Subscription> SubscribeToEventAsync(string eventType)
    {
        var response = await _httpClient.PostAsync(
            $"/api/v1/events/subscribe?agentId={_agentId}&eventType={eventType}", 
            null);
        response.EnsureSuccessStatusCode();
        
        var subscription = await response.Content.ReadFromJsonAsync<Subscription>(_jsonOptions);
        if (subscription == null)
            throw new InvalidOperationException("Failed to deserialize subscription");

        // Initialize Service Bus receiver if not already done
        if (_serviceBusClient == null)
        {
            _serviceBusClient = new ServiceBusClient(_serviceBusConnectionString);
            _receiver = _serviceBusClient.CreateReceiver(subscription.TopicName, subscription.SubscriptionId);
        }
        
        Log.Information("[ServiceBus] Subscribed to event type: {EventType}", eventType);
        return subscription;
    }

    public async Task PublishEventAsync(string eventType, object data, string? correlationId = null)
    {
        try
        {
            // Publishing goes through HTTP for simplicity in examples
            // In production, could send directly to Service Bus topic
            var envelope = new
            {
                eventId = Guid.NewGuid().ToString(),
                eventType,
                source = _agentId,
                timestamp = DateTime.UtcNow,
                dataVersion = "1.0",
                data,
                headers = correlationId != null 
                    ? new Dictionary<string, string> { ["correlationId"] = correlationId }
                    : null
            };

            var content = new StringContent(
                JsonSerializer.Serialize(envelope, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // 10 second timeout for publish
            var response = await _httpClient.PostAsync("/api/events/publish", content, cts.Token);
            response.EnsureSuccessStatusCode();
            
            Log.Information("[ServiceBus] Published event via HTTP: {EventType}", eventType);
        }
        catch (TaskCanceledException ex)
        {
            Log.Warning(ex, "Timeout publishing event {EventType} - event may still be delivered", eventType);
            throw new TimeoutException($"Timeout publishing event {eventType}", ex);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Failed to publish event {EventType}", eventType);
            throw;
        }
    }

    public async Task<EventEnvelope?> ReceiveEventAsync(string subscriptionId, int maxWaitSeconds, CancellationToken cancellationToken)
    {
        if (_receiver == null)
            throw new InvalidOperationException("No active subscription. Call SubscribeToAllEventsAsync first.");

        try
        {
            var message = await _receiver.ReceiveMessageAsync(
                TimeSpan.FromSeconds(maxWaitSeconds), 
                cancellationToken);

            if (message == null)
                return null;

            var body = message.Body.ToString();
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(body, _jsonOptions);
            
            // Complete the message
            await _receiver.CompleteMessageAsync(message, cancellationToken);
            
            return envelope;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ServiceBus] Error receiving event");
            throw;
        }
    }

    public async Task<AgentRegistration?> GetAgentAsync(string agentId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/agents/{agentId}");
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AgentRegistration>(_jsonOptions);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ServiceBus] Error getting agent {AgentId}", agentId);
            throw;
        }
    }

    public async Task SendDirectMessageAsync(string recipientAgentId, string message)
    {
        try
        {
            var envelope = new
            {
                messageId = Guid.NewGuid().ToString(),
                from = _agentId,
                to = recipientAgentId,
                message,
                timestamp = DateTime.UtcNow
            };

            var content = new StringContent(
                JsonSerializer.Serialize(envelope, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("/api/messages/send", content);
            response.EnsureSuccessStatusCode();
            
            Log.Information("[ServiceBus] Direct message sent from {From} to {To}", _agentId, recipientAgentId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ServiceBus] Error sending direct message to {Agent}", recipientAgentId);
            throw;
        }
    }

    public async Task<DirectMessage?> ReceiveDirectMessageAsync(int maxWaitSeconds, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/messages/receive?agentId={_agentId}&maxWaitSeconds={maxWaitSeconds}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DirectMessage>(_jsonOptions, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ServiceBus] Error receiving direct message");
            throw;
        }
    }

    public void Dispose()
    {
        _receiver?.DisposeAsync().AsTask().Wait();
        _serviceBusClient?.DisposeAsync().AsTask().Wait();
        _httpClient.Dispose();
    }
}
