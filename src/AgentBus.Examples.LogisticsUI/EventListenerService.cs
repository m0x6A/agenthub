using AgentBus.Examples.Shared;
using System.Text.Json;

namespace AgentBus.Examples.LogisticsUI;

/// <summary>
/// Background service that listens to all events from the broker and stores them for the UI
/// </summary>
public class EventListenerService : BackgroundService
{
    private readonly IAgentBusClient _agentBus;
    private readonly AgentCommunicationService _commService;
    private readonly ILogger<EventListenerService> _logger;

    public EventListenerService(
        IAgentBusClient agentBus,
        AgentCommunicationService commService,
        ILogger<EventListenerService> logger)
    {
        _agentBus = agentBus;
        _commService = commService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Register as a monitoring agent
            var registration = new AgentRegistration(
                Id: "logistics-ui",
                Name: "Logistics Coordination Dashboard",
                Version: "1.0.0",
                Capabilities: new[] { "monitoring", "visualization" },
                MessageTypes: new MessageTypes(new[] { "*" }, new string[] { }),
                Communication: new CommunicationCapabilities(false, true, false),
                EventsPublished: Array.Empty<string>(),
                Metadata: new AgentMetadata("logistics", "production", new[] { "ui", "monitoring" }));

            await _agentBus.RegisterAgentAsync(registration);
            _logger.LogInformation("✅ Logistics UI registered with broker");

            // Subscribe to ALL events
            var subscription = await _agentBus.SubscribeToAllEventsAsync();
            _logger.LogInformation("✅ Subscribed to all events: {SubscriptionId}", subscription.SubscriptionId);

            // Listen for events continuously
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var eventEnvelope = await _agentBus.ReceiveEventAsync(
                        subscription.SubscriptionId,
                        maxWaitSeconds: 30,
                        stoppingToken);

                    if (eventEnvelope != null)
                    {
                        // Store the event in our communication service
                        var dataString = eventEnvelope.Data.ValueKind == JsonValueKind.Undefined 
                            ? "" 
                            : eventEnvelope.Data.ToString();
                            
                        _commService.LogEvent(
                            eventType: eventEnvelope.EventType,
                            source: eventEnvelope.Source,
                            data: dataString,
                            timestamp: eventEnvelope.Timestamp);

                        _logger.LogInformation("📨 Event received: {EventType} from {Source}", 
                            eventEnvelope.EventType, eventEnvelope.Source);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error receiving event");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Event listener stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in event listener");
            throw;
        }
    }
}
