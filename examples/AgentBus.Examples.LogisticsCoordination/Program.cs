using AgentBus.Examples.Shared;
using AgentBus.Examples.LogisticsCoordination.Agents;
using Serilog;
using System.Text.Json;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

Console.WriteLine("\n" + new string('═', 120));
Console.WriteLine("🚚 LOGISTICS COORDINATION: TWO AUTONOMOUS AGENTS SOLVING PROBLEMS TOGETHER");
Console.WriteLine(new string('═', 120) + "\n");

try
{
    var brokerUrl = Environment.GetEnvironmentVariable("AGENTBUS_URL") ?? "http://localhost:5000";
    
    // Determine which agent to run
    if (args.Length == 0)
    {
        Console.WriteLine("Usage: dotnet run [ShippingAgent|WarehouseLead]");
        Console.WriteLine("\nExample:");
        Console.WriteLine("  Terminal 1: dotnet run WarehouseLead");
        Console.WriteLine("  Terminal 2: dotnet run ShippingAgent");
        return;
    }

    var agentMode = args[0];

    if (agentMode.Equals("ShippingAgent", StringComparison.OrdinalIgnoreCase))
    {
        await RunShippingAgentAsync(brokerUrl);
    }
    else if (agentMode.Equals("WarehouseLead", StringComparison.OrdinalIgnoreCase))
    {
        await RunWarehouseLeadAgentAsync(brokerUrl);
    }
    else
    {
        Log.Error("Unknown agent mode: {Mode}", agentMode);
    }
}
catch (HttpRequestException ex)
{
    Log.Fatal(ex, "❌ Cannot connect to AgentBus at {Url}", Environment.GetEnvironmentVariable("AGENTBUS_URL") ?? "http://localhost:5000");
    Log.Information("💡 Start broker: cd src/AgentBus.Broker && dotnet run");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Fatal error");
}
finally
{
    await Log.CloseAndFlushAsync();
}

// ============================================================================
// SHIPPING AGENT - Initiates problem-solving conversations
// ============================================================================
async Task RunShippingAgentAsync(string brokerUrl)
{
    using var agentBus = new HttpAgentBusClient(brokerUrl, "shipping-agent");

    var registration = new AgentRegistration(
        Id: "shipping-agent",
        Name: "Autonomous Shipping Agent",
        Version: "1.0.0",
        Capabilities: new[] { "route-planning", "carrier-selection", "cost-optimization" },
        MessageTypes: new MessageTypes(
            Accepts: new[] { "shipping.request", "warehouse.response" },
            Emits: new[] { "shipping.coordination.request", "shipping.decision.made" }),
        Communication: new CommunicationCapabilities(true, true, true),
        EventsPublished: new[] { "shipping.coordination.request", "shipping.decision.made" },
        Metadata: new AgentMetadata("logistics", "production", new[] { "shipping", "autonomous" }));

    await agentBus.RegisterAgentAsync(registration);
    var subscription = await agentBus.SubscribeToAllEventsAsync();

    var agent = new ShippingAgent(agentBus, Log.Logger);

    Log.Information("✅ SHIPPING AGENT registered and running");
    Log.Information("🚀 Role: Handles route planning, carrier selection, cost optimization");
    Log.Information("💡 Will ask Warehouse Lead Agent when needing inventory/consolidation advice\n");

    // Simulate a shipping request after a brief delay
    _ = Task.Run(async () =>
    {
        await Task.Delay(3000);
        
        Console.WriteLine("\n" + new string('═', 120));
        Console.WriteLine("📬 INCOMING SHIPPING REQUEST");
        Console.WriteLine(new string('═', 120) + "\n");

        await agentBus.PublishEventAsync("shipping.request", new
        {
            requestId = "SR-2024-001",
            productName = "Premium Electronics",
            quantity = 10,
            destination = "Tokyo",
            requiredHours = 24,
            customerTier = "VIP",
            budget = "flexible",
            urgency = "HIGH",
            notes = "Customer needs expedited delivery within 24 hours. Can consolidate from warehouses if needed to optimize costs."
        });
    });

    // Listen for events and process them
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    Log.Information("👂 Listening for shipping requests... (Ctrl+C to stop)\n");

    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            var eventEnvelope = await agentBus.ReceiveEventAsync(
                subscription.SubscriptionId,
                maxWaitSeconds: 30,
                cts.Token);

            if (eventEnvelope == null) continue;

            // Only process shipping requests and warehouse responses
            if (eventEnvelope.EventType == "shipping.request" || eventEnvelope.EventType == "warehouse.response")
            {
                Console.WriteLine(new string('═', 120));
                Log.Information("🧠 SHIPPING AGENT analyzing: {EventType}", eventEnvelope.EventType);
                Console.WriteLine(new string('─', 120));
                
                await agent.ProcessEventAsync(eventEnvelope);
                
                Console.WriteLine(new string('═', 120) + "\n");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(ex, "Error processing event");
        }
    }

    Log.Information("👋 Shipping Agent shutting down...");
}

// ============================================================================
// WAREHOUSE LEAD AGENT - Responds to coordination requests
// ============================================================================
async Task RunWarehouseLeadAgentAsync(string brokerUrl)
{
    using var agentBus = new HttpAgentBusClient(brokerUrl, "warehouse-lead-agent");

    var registration = new AgentRegistration(
        Id: "warehouse-lead-agent",
        Name: "Autonomous Warehouse Lead Agent",
        Version: "1.0.0",
        Capabilities: new[] { "inventory-management", "consolidation-planning", "fulfillment-optimization" },
        MessageTypes: new MessageTypes(
            Accepts: new[] { "shipping.coordination.request" },
            Emits: new[] { "warehouse.response", "warehouse.status.update" }),
        Communication: new CommunicationCapabilities(true, true, true),
        EventsPublished: new[] { "warehouse.response", "warehouse.status.update" },
        Metadata: new AgentMetadata("logistics", "production", new[] { "warehouse", "autonomous" }));

    await agentBus.RegisterAgentAsync(registration);
    var subscription = await agentBus.SubscribeToAllEventsAsync();

    var agent = new WarehouseLeadAgent(agentBus, Log.Logger);

    Log.Information("✅ WAREHOUSE LEAD AGENT registered and running");
    Log.Information("🏭 Role: Handles inventory management, consolidation, fulfillment optimization");
    Log.Information("💬 Responds to coordination requests from Shipping Agent\n");

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    Log.Information("👂 Listening for coordination requests from Shipping Agent... (Ctrl+C to stop)\n");

    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            var eventEnvelope = await agentBus.ReceiveEventAsync(
                subscription.SubscriptionId,
                maxWaitSeconds: 30,
                cts.Token);

            if (eventEnvelope == null) continue;

            // Only process shipping coordination requests
            if (eventEnvelope.EventType == "shipping.coordination.request")
            {
                Console.WriteLine(new string('═', 120));
                Log.Information("📩 WAREHOUSE LEAD receiving: {EventType}", eventEnvelope.EventType);
                Console.WriteLine(new string('─', 120));
                
                await agent.ProcessEventAsync(eventEnvelope);
                
                // After processing, publish a response event
                await agentBus.PublishEventAsync("warehouse.response", new
                {
                    responseToRequest = "shipping.coordination.request",
                    timestamp = DateTime.UtcNow,
                    agentDecision = "Agent processed request and is providing response via natural language in logs above"
                });
                
                Console.WriteLine(new string('═', 120) + "\n");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(ex, "Error processing request");
        }
    }

    Log.Information("👋 Warehouse Lead Agent shutting down...");
}
