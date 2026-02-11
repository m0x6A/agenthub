using AgentBus.Examples.Shared;
using AgentBus.Examples.Shared.ExternalSystems;
using AgentBus.Examples.Shared.Plugins;
using Microsoft.SemanticKernel;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

Console.WriteLine("\n" + new string('═', 100));
Console.WriteLine("🤖 AUTONOMOUS OPERATIONS & INVENTORY AGENT - LLM-Powered Supply Chain Optimization");
Console.WriteLine(new string('═', 100) + "\n");

try
{
    var agentId = "operations-inventory-agent";
    var brokerUrl = Environment.GetEnvironmentVariable("AGENTBUS_URL") ?? "http://localhost:5000";
    var serviceBusConn = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING");

    // Initialize AgentBus client
    IAgentBusClient agentBus;
    if (!string.IsNullOrEmpty(serviceBusConn))
    {
        agentBus = new ServiceBusAgentBusClient(brokerUrl, serviceBusConn, agentId);
        Log.Information("📡 Transport: Azure Service Bus");
    }
    else
    {
        agentBus = new HttpAgentBusClient(brokerUrl, agentId);
        Log.Information("📡 Transport: HTTP (set SERVICEBUS_CONNECTION_STRING for Service Bus)");
    }

    var inventoryDb = new MockInventoryDatabase();

    var registration = new AgentRegistration(
        Id: agentId,
        Name: "Autonomous Operations & Inventory Agent",
        Version: "2.0.0",
        Capabilities: new[] { "autonomous-reasoning", "inventory", "fulfillment-optimization" },
        MessageTypes: new MessageTypes(new[] { "customer.inquiry.analyzed" }, new[] { "operations.inventory.checked" }),
        Communication: new CommunicationCapabilities(false, true, true),
        EventsPublished: new[] { "operations.inventory.checked" },
        Metadata: new AgentMetadata("demo", "dev", new[] { "autonomous", "supply-chain" }));

    await agentBus.RegisterAgentAsync(registration);
    var subscription = await agentBus.SubscribeToAllEventsAsync();

    var agent = new OperationsInventoryAgent(agentId, agentBus, inventoryDb, Log.Logger);

    Log.Information("✅ Autonomous agent registered");
    Log.Information("🧠 Mode: AUTONOMOUS - LLM optimizes fulfillment strategies");
    Log.Information("🔧 Tools: Inventory (check/reserve), Fulfillment Strategy, AgentBus");
    Log.Information("💡 Set OPENAI_API_KEY for real LLM reasoning\n");

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    Log.Information("👂 Listening for events...\n");

    // Keep-alive heartbeat
    _ = Task.Run(async () =>
    {
        while (!cts.Token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cts.Token);
            if (!cts.Token.IsCancellationRequested)
            {
                Log.Information("💓 Agent alive and listening...");
            }
        }
    });

    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            var eventEnvelope = await agentBus.ReceiveEventAsync(subscription.SubscriptionId, 30, cts.Token);
            if (eventEnvelope == null)
            {
                continue;
            }

            Console.WriteLine(new string('═', 100));
            await agent.ProcessEventAsync(eventEnvelope);
            Console.WriteLine(new string('═', 100) + "\n");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(ex, "Error processing event");
        }
    }

    Log.Information("\n👋 Shutting down...");
    agentBus.Dispose();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Fatal error");
}
finally
{
    await Log.CloseAndFlushAsync();
}

class OperationsInventoryAgent : AutonomousAgent
{
    public OperationsInventoryAgent(
        string agentId,
        IAgentBusClient agentBus,
        MockInventoryDatabase inventoryDb,
        ILogger logger)
        : base(
            agentId,
            systemPrompt: @"You are an AUTONOMOUS Operations & Inventory Agent that DECIDES optimal fulfillment strategies.

YOUR ROLE: Manage inventory and optimize order fulfillment, not by rules, but by REASONING.

TOOLS:
- Inventory.check_inventory: Check stock levels
- Inventory.reserve_inventory: Reserve products
- Inventory.determine_fulfillment_strategy: Optimize shipping
- AgentBus.publish_event: Coordinate with other agents

AUTONOMOUS BEHAVIOR:
When you see a customer inquiry:
1. ANALYZE: What products? Quantity? Customer urgency?
2. INVESTIGATE: Check inventory levels
3. STRATEGIZE: Single warehouse vs split shipment? Cost vs speed trade-off?
4. DECIDE: Can fulfill? Should reserve now or wait for payment auth?
5. COORDINATE: Inform Financial agent if high-value (>$500)

EXAMPLE - Inquiry for 2 Premium Widgets:
  Reasoning: ""Premium items are high-value. Check inventory across all locations. 
              If available, determine best warehouse. Coordinate with Financial for payment before reserving.""
  Actions: 1) check_inventory for each SKU, 2) determine_fulfillment_strategy, 3) publish 'operations.inventory.checked' with findings

You optimize for delivery time AND cost. You THINK about trade-offs.",
            agentBus,
            logger)
    {
        Kernel.Plugins.AddFromObject(new InventoryPlugin(inventoryDb), "Inventory");
    }

    protected override string GetModelId() => "gpt-4o-mini";

    protected override void ConfigurePlugins(Kernel kernel)
    {
        kernel.Plugins.AddFromObject(new AgentBusPlugin(AgentBus, AgentId), "AgentBus");
    }
}
