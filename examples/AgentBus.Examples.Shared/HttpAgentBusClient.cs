using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Serilog;

namespace AgentBus.Examples.Shared;

/// <summary>
/// HTTP-based AgentBus client for REST API communication
/// </summary>
public class HttpAgentBusClient : IAgentBusClient
{
    private readonly HttpClient _httpClient;
    private readonly string _agentId;
    private readonly JsonSerializerOptions _jsonOptions;

    public HttpAgentBusClient(string baseUrl, string agentId)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _agentId = agentId;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task RegisterAgentAsync(AgentRegistration registration)
    {
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

                var response = await _httpClient.PostAsync("/api/agents/register", content);
                response.EnsureSuccessStatusCode();
                
                Log.Information("[HTTP] Agent registered: {AgentId}", registration.Id);
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
        var response = await _httpClient.PostAsync(
            $"/api/events/subscribe/all?agentId={_agentId}", 
            null);
        response.EnsureSuccessStatusCode();
        
        var subscription = await response.Content.ReadFromJsonAsync<Subscription>(_jsonOptions);
        if (subscription == null)
            throw new InvalidOperationException("Failed to deserialize subscription");
            
        Log.Information("[HTTP] Subscribed to global events: {SubscriptionId}", subscription.SubscriptionId);
        return subscription;
    }

    public async Task<Subscription> SubscribeToEventAsync(string eventType)
    {
        var response = await _httpClient.PostAsync(
            $"/api/events/subscribe?agentId={_agentId}&eventType={eventType}", 
            null);
        response.EnsureSuccessStatusCode();
        
        var subscription = await response.Content.ReadFromJsonAsync<Subscription>(_jsonOptions);
        if (subscription == null)
            throw new InvalidOperationException("Failed to deserialize subscription");
            
        Log.Information("[HTTP] Subscribed to event type: {EventType}", eventType);
        return subscription;
    }

    public async Task PublishEventAsync(string eventType, object data, string? correlationId = null)
    {
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

        var response = await _httpClient.PostAsync("/api/events/publish", content);
        response.EnsureSuccessStatusCode();
        
        Log.Debug("[HTTP] Published event: {EventType}", eventType);
    }

    public async Task<EventEnvelope?> ReceiveEventAsync(string subscriptionId, int maxWaitSeconds, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/events/receive/{subscriptionId}?maxWaitSeconds={maxWaitSeconds}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EventEnvelope>(_jsonOptions, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[HTTP] Error receiving event");
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
            Log.Error(ex, "[HTTP] Error getting agent {AgentId}", agentId);
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
            
            Log.Information("[HTTP] Direct message sent from {From} to {To}", _agentId, recipientAgentId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[HTTP] Error sending direct message to {Agent}", recipientAgentId);
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
            Log.Error(ex, "[HTTP] Error receiving direct message");
            throw;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
