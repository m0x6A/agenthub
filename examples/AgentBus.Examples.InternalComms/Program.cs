using System.Text.Json;
using Serilog;
using AgentBus.Examples.Shared;
using AgentBus.Examples.Shared.ExternalSystems;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

Console.WriteLine("\n" + new string('═', 90));
Console.WriteLine("💬 INTERNAL COMMUNICATIONS AGENT - TEAMS INTEGRATION");
Console.WriteLine(new string('═', 90));

// Configuration
var agentId = "internal-communications-agent";
var agentBusUrl = Environment.GetEnvironmentVariable("AGENTBUS_URL") ?? "http://localhost:5000";
var serviceBusConnStr = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING") ?? "mock-connection";

bool useServiceBus = serviceBusConnStr != "mock-connection";
Log.Information("Transport: {Transport}", useServiceBus ? "Azure Service Bus" : "HTTP (Service Bus not configured)");
Log.Information("External System: Microsoft Teams API (Mock)");

// Initialize external system
var teamsApi = new MockTeamsApi();
var eventLog = new List<string>();

// Initialize AgentBus client
IAgentBusClient agentBusClient = useServiceBus
    ? new ServiceBusAgentBusClient(agentBusUrl, serviceBusConnStr, agentId)
    : new HttpAgentBusClient(agentBusUrl, agentId);

try
{
    // Register Agent
    Log.Information("📝 Registering with AgentBus...");
    await agentBusClient.RegisterAgentAsync(new AgentRegistration(
        Id: agentId,
        Name: "Internal Communications Agent",
        Version: "1.0.0",
        Capabilities: new[] { "teams-notifications", "audit-logging", "alert-routing" },
        MessageTypes: new MessageTypes(
            Accepts: new[] { "*" },  // Subscribe to all events
            Emits: new[] { "teams.notification.sent", "audit.log.created" }),
        Communication: new CommunicationCapabilities(false, true, true),
        EventsPublished: new[] { "teams.notification.sent" },
        Metadata: new AgentMetadata("it-operations", "demo", new[] { useServiceBus ? "servicebus-transport" : "http-transport" })));

    Log.Information("✅ Registered!");

    // Subscribe to global events
    Log.Information("🔔 Subscribing to global events (all event types)...");
    var subscription = await agentBusClient.SubscribeToAllEventsAsync();
    Log.Information("✅ Subscribed: {SubscriptionId}", subscription.SubscriptionId);

    Console.WriteLine("\n📢 Monitoring Channels:");
    Console.WriteLine("   • operations-alerts");
    Console.WriteLine("   • finance-team");
    Console.WriteLine("   • customer-service");
    Console.WriteLine("   • management-dashboard");
    Console.WriteLine(new string('═', 90) + "\n");

    // Listen for events
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    await ListenForEventsAsync(agentBusClient, teamsApi, eventLog, subscription.SubscriptionId, cts.Token);
}
catch (HttpRequestException ex)
{
    Log.Fatal(ex, "❌ Cannot connect to AgentBus at {Url}", agentBusUrl);
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Fatal error");
}
finally
{
    agentBusClient.Dispose();
}

static async Task ListenForEventsAsync(IAgentBusClient client, MockTeamsApi teams, List<string> log, string subId, CancellationToken ct)
{
    Log.Information("👂 Listening for ALL events...\n");
    
    while (!ct.IsCancellationRequested)
    {
        try
        {
            var evt = await client.ReceiveEventAsync(subId, 30, ct);
            
            if (evt != null)
            {
                Console.WriteLine($"\n📨 {evt.EventType} from {evt.Source} at {evt.Timestamp:HH:mm:ss}");
                
                // Log event
                log.Add($"[{evt.Timestamp:HH:mm:ss}] {evt.EventType} from {evt.Source}");
                
                // Route to appropriate Teams channel based on event type
                switch (evt.EventType)
                {
                    case "customer.inquiry.received":
                        await teams.PostToChannelAsync(
                            "customer-service",
                            "New Customer Inquiry",
                            $"Customer requesting order modification.\nOrder: {evt.Data.GetProperty("orderId").GetString()}\nSKU: {evt.Data.GetProperty("requestedSku").GetString()}\nQuantity: {evt.Data.GetProperty("requestedQuantity").GetInt32()}",
                            "normal");
                        break;
                        
                    case "operations.inventory.checked":
                        var available = evt.Data.GetProperty("available").GetBoolean();
                        await teams.PostToChannelAsync(
                            "operations-alerts",
                            available ? "Inventory Reserved" : "Inventory Issue",
                            available 
                                ? $"✅ Reserved {evt.Data.GetProperty("quantity").GetInt32()}x {evt.Data.GetProperty("sku").GetString()}\nWarehouse: {evt.Data.GetProperty("warehouseLocation").GetString()}\nShip ETA: {evt.Data.GetProperty("estimatedShipDays").GetInt32()} days"
                                : $"⚠️  Insufficient inventory for requested items",
                            available ? "normal" : "high");
                        break;
                        
                    case "financial.authorization.completed":
                        var amount = evt.Data.GetProperty("amount").GetDecimal();
                        var status = evt.Data.GetProperty("status").GetString();
                        await teams.PostToChannelAsync(
                            "finance-team",
                            "Payment Authorization",
                            $"Status: {status?.ToUpper()}\nAmount: ${amount:F2}\nTransaction: {evt.Data.GetProperty("transactionId").GetString()}\nRisk Score: {evt.Data.GetProperty("riskScore").GetInt32()}/100",
                            amount > 500 ? "high" : "normal");
                        break;
                        
                    case "order.confirmed":
                        await teams.PostToChannelAsync(
                            "customer-service",
                            "Order Confirmed",
                            $"✅ Order {evt.Data.GetProperty("orderId").GetString()} confirmed\nTotal: ${evt.Data.GetProperty("finalAmount").GetDecimal():F2}\nPayment Auth: {evt.Data.GetProperty("paymentAuthCode").GetString()}",
                            "normal");
                        
                        // Post summary to management dashboard
                        await GenerateManagementSummary(client, teams, log);
                        break;
                }
                
                Console.WriteLine(new string('━', 90));
            }
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex) { Log.Error(ex, "Error"); await Task.Delay(2000, ct); }
    }
}

static async Task GenerateManagementSummary(IAgentBusClient client, MockTeamsApi teams, List<string> eventLog)
{
    var summary = $@"**Transaction Flow Completed**

Timeline:
{string.Join("\n", eventLog.TakeLast(5))}

**Summary:**
• Customer inquiry processed
• Inventory checked and reserved
• Payment authorized successfully
• Order confirmed

Total Events: {eventLog.Count}";

    await teams.PostAdaptiveCardAsync(
        "management-dashboard",
        "Order Processing Complete",
        new Dictionary<string, string>
        {
            ["Status"] = "✅ Completed",
            ["Agent Collaboration"] = "3 agents",
            ["Duration"] = "< 1 minute",
            ["Outcome"] = "Order confirmed"
        });

    await client.PublishEventAsync("audit.log.created", new
    {
        summary,
        eventCount = eventLog.Count,
        timestamp = DateTime.UtcNow
    });

    Console.WriteLine("\n📊 Management dashboard updated");
}
