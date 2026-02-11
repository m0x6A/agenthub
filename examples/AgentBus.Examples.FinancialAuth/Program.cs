using System.Text.Json;
using Microsoft.SemanticKernel;
using Serilog;
using AgentBus.Examples.Shared;
using AgentBus.Examples.Shared.ExternalSystems;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

Console.WriteLine("\n" + new string('═', 90));
Console.WriteLine("💰 FINANCIAL AUTHORIZATION AGENT - PAYMENT & COMPLIANCE");
Console.WriteLine(new string('═', 90));

// Configuration
var agentId = "financial-authorization-agent";
var agentBusUrl = Environment.GetEnvironmentVariable("AGENTBUS_URL") ?? "http://localhost:5000";

Log.Information("Transport: HTTP");
Log.Information("External System: Payment Gateway (Mock)");
Log.Information("Model: Claude 3.5 Sonnet (conceptual - using mock)");

// Initialize external system
var paymentGateway = new MockPaymentGateway();

// Initialize AgentBus client (HTTP transport)
using var agentBusClient = new HttpAgentBusClient(agentBusUrl, agentId);

try
{
    // Register Agent
    Log.Information("📝 Registering with AgentBus...");
    await agentBusClient.RegisterAgentAsync(new AgentRegistration(
        Id: agentId,
        Name: "Financial Authorization Agent",
        Version: "1.0.0",
        Capabilities: new[] { "payment-authorization", "pricing-calculation", "fraud-detection" },
        MessageTypes: new MessageTypes(
            Accepts: new[] { "operations.inventory.checked" },
            Emits: new[] { "financial.authorization.completed", "order.confirmed" }),
        Communication: new CommunicationCapabilities(false, true, true),
        EventsPublished: new[] { "financial.authorization.completed", "order.confirmed" },
        Metadata: new AgentMetadata("finance", "demo", new[] { "http-transport" })));

    Log.Information("✅ Registered!");

    // Subscribe to global events
    Log.Information("🔔 Subscribing to global events...");
    var subscription = await agentBusClient.SubscribeToAllEventsAsync();
    Log.Information("✅ Subscribed: {SubscriptionId}", subscription.SubscriptionId);

    Console.WriteLine(new string('═', 90) + "\n");

    // Listen for events
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    await ListenForEventsAsync(agentBusClient, paymentGateway, subscription.SubscriptionId, cts.Token);
}
catch (HttpRequestException ex)
{
    Log.Fatal(ex, "❌ Cannot connect to AgentBus at {Url}", agentBusUrl);
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Fatal error");
}

static async Task ListenForEventsAsync(IAgentBusClient client, MockPaymentGateway gateway, string subId, CancellationToken ct)
{
    Log.Information("👂 Listening for inventory checks...\n");
    
    while (!ct.IsCancellationRequested)
    {
        try
        {
            var evt = await client.ReceiveEventAsync(subId, 30, ct);
            
            if (evt != null)
            {
                Console.WriteLine($"\n📨 {evt.EventType} from {evt.Source} at {evt.Timestamp:HH:mm:ss}");
                
                if (evt.EventType == "operations.inventory.checked")
                {
                    var data = evt.Data;
                    var available = data.TryGetProperty("available", out var avail) && avail.GetBoolean();
                    
                    if (!available)
                    {
                        Console.WriteLine("⏭️  Skipping - inventory not available");
                        continue;
                    }
                    
                    Console.WriteLine("💳 Processing payment authorization...");
                    
                    // Extract pricing data
                    var quantity = data.GetProperty("quantity").GetInt32();
                    var unitPrice = data.GetProperty("unitPrice").GetDecimal();
                    var shippingCost = data.GetProperty("shippingCost").GetDecimal();
                    
                    // Calculate totals
                    var subtotal = quantity * unitPrice;
                    var tax = subtotal * 0.08m; // 8% tax
                    var total = subtotal + tax + shippingCost;
                    
                    Console.WriteLine($"\n💵 Pricing Calculation:");
                    Console.WriteLine($"   Subtotal: ${subtotal:F2} ({quantity} × ${unitPrice:F2})");
                    Console.WriteLine($"   Tax (8%): ${tax:F2}");
                    Console.WriteLine($"   Shipping: ${shippingCost:F2}");
                    Console.WriteLine($"   Total: ${total:F2}");
                    
                    // Authorize payment via external system
                    var authResult = await gateway.AuthorizePaymentAsync("C12345", total);
                    
                    Console.WriteLine($"\n🔐 Payment Authorization:");
                    Console.WriteLine($"   Status: {authResult.Status.ToUpper()}");
                    Console.WriteLine($"   Transaction ID: {authResult.TransactionId}");
                    Console.WriteLine($"   Auth Code: {authResult.AuthorizationCode}");
                    Console.WriteLine($"   Risk Score: {authResult.RiskScore}/100");
                    Console.WriteLine($"   Message: {authResult.Message}");
                    
                    if (authResult.Status == "approved")
                    {
                        // Publish authorization result
                        await client.PublishEventAsync("financial.authorization.completed", new
                        {
                            transactionId = authResult.TransactionId,
                            authorizationCode = authResult.AuthorizationCode,
                            status = "approved",
                            amount = total,
                            riskScore = authResult.RiskScore,
                            breakdown = new
                            {
                                subtotal,
                                tax,
                                shipping = shippingCost,
                                total
                            }
                        });
                        
                        Console.WriteLine("\n✅ Published: financial.authorization.completed");
                        
                        // Also publish order confirmation
                        await client.PublishEventAsync("order.confirmed", new
                        {
                            orderId = "ORD-2024-001",
                            status = "confirmed",
                            finalAmount = total,
                            paymentAuthCode = authResult.AuthorizationCode,
                            estimatedDelivery = DateTime.UtcNow.AddDays(1),
                            timestamp = DateTime.UtcNow
                        });
                        
                        Console.WriteLine("✅ Published: order.confirmed");
                    }
                    else
                    {
                        Console.WriteLine($"\n⚠️  Payment {authResult.Status} - order not confirmed");
                    }
                }
                
                Console.WriteLine(new string('━', 90));
            }
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex) { Log.Error(ex, "Error"); await Task.Delay(2000, ct); }
    }
}
