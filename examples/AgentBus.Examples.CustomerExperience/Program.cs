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
Console.WriteLine("🎯 CUSTOMER EXPERIENCE AGENT - E-COMMERCE PLATFORM");
Console.WriteLine(new string('═', 90));

// Configuration
var agentId = "customer-experience-agent";
var agentBusUrl = Environment.GetEnvironmentVariable("AGENTBUS_URL") ?? "http://localhost:5000";
var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var modelId = "gpt-4o";

Log.Information("Transport: HTTP");
Log.Information("External System: Order Management System (Mock)");
Log.Information("Model: {Model}", modelId);

// Initialize Semantic Kernel if API key available
Kernel? kernel = null;
IChatCompletionService? chatService = null;

if (!string.IsNullOrEmpty(openAiKey))
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(modelId, openAiKey);
    kernel = kernelBuilder.Build();
    chatService = kernel.GetRequiredService<IChatCompletionService>();
    Log.Information("✅ Semantic Kernel initialized with real LLM");
}
else
{
    Log.Warning("⚠️  No OPENAI_API_KEY - using mock responses");
}

var systemPrompt = @"Extract order details from customer requests as JSON:
{""intent"":""order_modification"",""order_id"":""ORD-XXX"",""product_sku"":""SKU-XXX"",""quantity"":N,""sentiment"":""positive|neutral|negative"",""urgency"":""low|normal|high""}";

// Initialize external system
var orderSystem = new MockOrderSystemApi();

// Initialize AgentBus client (HTTP transport)
using var agentBusClient = new HttpAgentBusClient(agentBusUrl, agentId);

try
{
    // Register Agent
    Log.Information("📝 Registering with AgentBus...");
    await agentBusClient.RegisterAgentAsync(new AgentRegistration(
        Id: agentId,
        Name: "Customer Experience Agent",
        Version: "1.0.0",
        Capabilities: new[] { "customer-inquiry", "intent-classification", "order-management" },
        MessageTypes: new MessageTypes(
            Accepts: new[] { "order.confirmed" },
            Emits: new[] { "customer.inquiry.received" }),
        Communication: new CommunicationCapabilities(false, true, true),
        EventsPublished: new[] { "customer.inquiry.received" },
        Metadata: new AgentMetadata("customer-service", "demo", new[] { "http-transport" })));

    Log.Information("✅ Registered!");

    // Subscribe to global events
    Log.Information("🔔 Subscribing to global events...");
    var subscription = await agentBusClient.SubscribeToAllEventsAsync();
    Log.Information("✅ Subscribed: {SubscriptionId}", subscription.SubscriptionId);

    Console.WriteLine(new string('═', 90) + "\n");

    // Simulate customer inquiry
    _ = Task.Run(async () =>
    {
        await Task.Delay(3000);
        await SimulateCustomerInquiry(agentBusClient, orderSystem, chatService, systemPrompt);
    });

    // Listen for events
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    await ListenForEventsAsync(agentBusClient, chatService, subscription.SubscriptionId, cts.Token);
}
catch (HttpRequestException ex)
{
    Log.Fatal(ex, "❌ Cannot connect to AgentBus at {Url}", agentBusUrl);
    Log.Information("💡 Start broker: cd src/AgentBus.Broker && dotnet run");
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Fatal error");
}

static async Task SimulateCustomerInquiry(IAgentBusClient client, MockOrderSystemApi orderSystem, IChatCompletionService? chat, string prompt)
{
    var msg = "Hi! I'd like to add 2 more Premium Widget Plus (SKU-789) to order ORD-2024-001. Can you check availability?";
    Console.WriteLine("\n📬 NEW CUSTOMER INQUIRY\n" + new string('━', 90));
    Console.WriteLine($"💬 {msg}\n" + new string('━', 90));

    var order = await orderSystem.GetOrderAsync("ORD-2024-001");
    if (order != null)
    {
        Console.WriteLine($"\n📦 Current Order: {order.OrderId} | Total: ${order.Total:F2} | Items: {order.Items.Length}");
    }

    await client.PublishEventAsync("customer.inquiry.received", new
    {
        customerId = "C12345",
        orderId = "ORD-2024-001",
        requestedSku = "SKU-789",
        requestedQuantity = 2,
        productName = "Premium Widget Plus"
    });

    Console.WriteLine("\n✅ Published: customer.inquiry.received\n" + new string('═', 90));
}

static async Task ListenForEventsAsync(IAgentBusClient client, IChatCompletionService? chat, string subId, CancellationToken ct)
{
    Log.Information("👂 Listening...\n");
    while (!ct.IsCancellationRequested)
    {
        try
        {
            var evt = await client.ReceiveEventAsync(subId, 30, ct);
            if (evt != null)
            {
                Console.WriteLine($"\n📨 {evt.EventType} from {evt.Source} at {evt.Timestamp:HH:mm:ss}");
                if (evt.EventType == "order.confirmed")
                {
                    Console.WriteLine("🎉 Order Confirmed!");
                    Console.WriteLine($"📦 {JsonSerializer.Serialize(evt.Data, new JsonSerializerOptions { WriteIndented = true })}");
                    Console.WriteLine("\n💬 Customer Response: Great news! Your order update is confirmed and payment authorized.");
                }
            }
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex) { Log.Error(ex, "Error"); await Task.Delay(2000, ct); }
    }
}
