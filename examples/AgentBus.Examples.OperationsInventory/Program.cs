using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Serilog;
using AgentBus.Examples.Shared;
using AgentBus.Examples.Shared.ExternalSystems;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

Console.WriteLine("\n" + new string('═', 90));
Console.WriteLine("📦 OPERATIONS & INVENTORY AGENT - SUPPLY CHAIN MANAGEMENT");
Console.WriteLine(new string('═', 90));

// Configuration
var agentId = "operations-inventory-agent";
var agentBusUrl = Environment.GetEnvironmentVariable("AGENTBUS_URL") ?? "http://localhost:5000";
var serviceBusConnStr = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING") ?? "mock-connection";
var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var modelId = "gpt-4o-mini";

bool useServiceBus = serviceBusConnStr != "mock-connection";
Log.Information("Transport: {Transport}", useServiceBus ? "Azure Service Bus" : "HTTP (Service Bus not configured)");
Log.Information("External System: Inventory Database (Mock)");
Log.Information("Model: {Model}", modelId);

// Initialize Semantic Kernel if API key available
IChatCompletionService? chatService = null;
if (!string.IsNullOrEmpty(openAiKey))
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(modelId, openAiKey);
    var kernel = kernelBuilder.Build();
    chatService = kernel.GetRequiredService<IChatCompletionService>();
    Log.Information("✅ Semantic Kernel initialized");
}
else
{
    Log.Warning("⚠️  No OPENAI_API_KEY - using mock responses");
}

// Initialize external system
var inventoryDb = new MockInventoryDatabase();

// Initialize AgentBus client (Service Bus if available, otherwise HTTP)
IAgentBusClient agentBusClient = useServiceBus
    ? new ServiceBusAgentBusClient(agentBusUrl, serviceBusConnStr, agentId)
    : new HttpAgentBusClient(agentBusUrl, agentId);

try
{
    // Register Agent
    Log.Information("📝 Registering with AgentBus...");
    await agentBusClient.RegisterAgentAsync(new AgentRegistration(
        Id: agentId,
        Name: "Operations & Inventory Agent",
        Version: "1.0.0",
        Capabilities: new[] { "inventory-check", "fulfillment-planning", "warehouse-routing" },
        MessageTypes: new MessageTypes(
            Accepts: new[] { "customer.inquiry.received" },
            Emits: new[] { "operations.inventory.checked" }),
        Communication: new CommunicationCapabilities(false, true, true),
        EventsPublished: new[] { "operations.inventory.checked" },
        Metadata: new AgentMetadata("operations", "demo", new[] { useServiceBus ? "servicebus-transport" : "http-transport" })));

    Log.Information("✅ Registered!");

    // Subscribe to global events
    Log.Information("🔔 Subscribing to global events...");
    var subscription = await agentBusClient.SubscribeToAllEventsAsync();
    Log.Information("✅ Subscribed: {SubscriptionId}", subscription.SubscriptionId);

    Console.WriteLine(new string('═', 90) + "\n");

    // Listen for events
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    await ListenForEventsAsync(agentBusClient, inventoryDb, subscription.SubscriptionId, cts.Token);
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

static async Task ListenForEventsAsync(IAgentBusClient client, MockInventoryDatabase inventoryDb, string subId, CancellationToken ct)
{
    Log.Information("👂 Listening for customer inquiries...\n");
    
    while (!ct.IsCancellationRequested)
    {
        try
        {
            var evt = await client.ReceiveEventAsync(subId, 30, ct);
            
            if (evt != null)
            {
                Console.WriteLine($"\n📨 {evt.EventType} from {evt.Source} at {evt.Timestamp:HH:mm:ss}");
                
                if (evt.EventType == "customer.inquiry.received")
                {
                    Console.WriteLine("🔍 Processing inventory check...");
                    
                    // Extract data
                    var data = evt.Data;
                    var sku = data.GetProperty("requestedSku").GetString() ?? "";
                    var quantity = data.GetProperty("requestedQuantity").GetInt32();
                    
                    // Check inventory in external system
                    var inventoryItem = await inventoryDb.CheckInventoryAsync(sku);
                    
                    if (inventoryItem == null)
                    {
                        Console.WriteLine($"❌ SKU {sku} not found");
                        continue;
                    }
                    
                    Console.WriteLine($"\n📊 Inventory Status:");
                    Console.WriteLine($"   SKU: {inventoryItem.Sku}");
                    Console.WriteLine($"   Product: {inventoryItem.ProductName}");
                    Console.WriteLine($"   Available: {inventoryItem.QuantityAvailable} units");
                    Console.WriteLine($"   Unit Price: ${inventoryItem.UnitPrice}");
                    Console.WriteLine($"   Warehouse: {inventoryItem.WarehouseLocation}");
                    
                    bool available = inventoryItem.QuantityAvailable >= quantity;
                    
                    if (available)
                    {
                        // Reserve inventory
                        var reservationId = await inventoryDb.ReserveInventoryAsync(sku, quantity);
                        
                        // Determine fulfillment
                        var fulfillment = await inventoryDb.DetermineFulfillmentAsync(new[] { sku }, "98101");
                        
                        Console.WriteLine($"\n✅ Inventory Available!");
                        Console.WriteLine($"   Reserved: {reservationId}");
                        Console.WriteLine($"   Ship from: {fulfillment.WarehouseCode}");
                        Console.WriteLine($"   ETA: {fulfillment.EstimatedShippingDays} days");
                        
                        // Publish result to AgentBus
                        await client.PublishEventAsync("operations.inventory.checked", new
                        {
                            available = true,
                            sku,
                            quantity,
                            productName = inventoryItem.ProductName,
                            unitPrice = inventoryItem.UnitPrice,
                            warehouseLocation = fulfillment.WarehouseCode,
                            estimatedShipDays = fulfillment.EstimatedShippingDays,
                            shippingCost = fulfillment.ShippingCost,
                            reservationId,
                            timestamp = DateTime.UtcNow
                        });
                        
                        Console.WriteLine("\n✅ Published: operations.inventory.checked");
                    }
                    else
                    {
                        Console.WriteLine($"\n❌ Insufficient inventory (need {quantity}, have {inventoryItem.QuantityAvailable})");
                    }
                }
                
                Console.WriteLine(new string('━', 90));
            }
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex) { Log.Error(ex, "Error"); await Task.Delay(2000, ct); }
    }
}
