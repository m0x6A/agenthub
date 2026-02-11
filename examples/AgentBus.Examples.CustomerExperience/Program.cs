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
Console.WriteLine("🤖 AUTONOMOUS CUSTOMER EXPERIENCE AGENT - LLM-Powered Decision Making");
Console.WriteLine(new string('═', 100) + "\n");

try
{
    var agentId = "customer-experience-agent";
    var brokerUrl = Environment.GetEnvironmentVariable("AGENTBUS_URL") ?? "http://localhost:5000";

    // Initialize AgentBus client (HTTP transport)
    using var agentBus = new HttpAgentBusClient(brokerUrl, agentId);

    // Initialize external system (mocked)
    var orderSystem = new MockOrderSystemApi();

    // Register agent
    var registration = new AgentRegistration(
        Id: agentId,
        Name: "Autonomous Customer Experience Agent",
        Version: "2.0.0",
        Capabilities: new[] { "autonomous-reasoning", "customer-service", "llm-decisions" },
        MessageTypes: new MessageTypes(new[] { "customer.*", "order.confirmed" }, new[] { "customer.inquiry.analyzed" }),
        Communication: new CommunicationCapabilities(false, true, true),
        EventsPublished: new[] { "customer.inquiry.analyzed" },
        Metadata: new AgentMetadata("demo", "dev", new[] { "autonomous", "http-transport" }));

    await agentBus.RegisterAgentAsync(registration);

    // Subscribe to relevant events
    var subscription = await agentBus.SubscribeToAllEventsAsync();

    // Create autonomous agent
    var agent = new CustomerExperienceAgent(agentId, agentBus, orderSystem, Log.Logger);

    Log.Information("✅ Autonomous agent registered");
    Log.Information("🧠 Mode: AUTONOMOUS - LLM analyzes events and chooses actions");
    Log.Information(" 🔧 Tools: OrderSystem (get/update/confirm), AgentBus (publish)");
    Log.Information("📡 Transport: HTTP REST API");
    Log.Information("💡 Set OPENAI_API_KEY for real LLM reasoning\n");

    // Simulate a customer inquiry
    _ = Task.Run(async () =>
    {
        await Task.Delay(2000);
        Console.WriteLine("📬 SIMULATING CUSTOMER INQUIRY");
        Console.WriteLine(new string('─', 100));
        await agentBus.PublishEventAsync("customer.inquiry.received", new
        {
            customerId = "CUST-001",
            orderId = "ORD-2024-001",
            message = "I need to check my order status urgently",
            sentiment = "concerned"
        });
    });

    // Event processing loop
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    Log.Information("👂 Listening for events... (Ctrl+C to stop)\n");

    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            var eventEnvelope = await agentBus.ReceiveEventAsync(
                subscription.SubscriptionId, 
                maxWaitSeconds: 30, 
                cts.Token);
            
            if (eventEnvelope == null) continue;

            Console.WriteLine(new string('═', 100));
            
            // AUTONOMOUS PROCESSING - Agent decides what to do
            await agent.ProcessEventAsync(eventEnvelope);
            
            Console.WriteLine(new string('═', 100) + "\n");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(ex, "Error processing event");
        }
    }

    Log.Information("\n👋 Shutting down...");
}
catch (HttpRequestException ex)
{
    Log.Fatal(ex, "❌ Cannot connect to AgentBus");
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

/// <summary>
/// TRULY AUTONOMOUS AGENT - Uses LLM to make decisions, not hardcoded logic
/// </summary>
class CustomerExperienceAgent : AutonomousAgent
{
    public CustomerExperienceAgent(
        string agentId,
        IAgentBusClient agentBus,
        MockOrderSystemApi orderSystem,
        ILogger logger)
        : base(
            agentId,
            systemPrompt: @"You are an AUTONOMOUS Customer Experience Agent with REAL decision-making power.

YOUR ROLE: Help customers with orders, not by following scripts, but by REASONING about each situation.

TOOLS YOU CAN CHOOSE TO USE:
- OrderSystem.get_order_details: Retrieve order information
- OrderSystem.update_order: Modify an order (add items)
- OrderSystem.confirm_order_fulfillment: Final order confirmation (needs payment auth code)
- AgentBus.publish_event: Communicate with other agents

HOW TO BE AUTONOMOUS:
1. ANALYZE each event: Is it relevant? What does the customer need?
2. DECIDE: What information do I need? Which tools help?
3. EXECUTE: Call the tools you choose
4. COORDINATE: If you need help from other agents (Operations for inventory, Financial for payment), publish events

EXAMPLE - Customer asks ""Where's my order?"":
  Your reasoning: ""Need order details. If there's an issue, may need other agents' help.""
  Your actions: 1) get_order_details, 2) Analyze status, 3) If inventory issue, publish 'customer.inquiry.analyzed' for Operations agent

You are NOT a microservice. You THINK and DECIDE. Use tools wisely.",
            agentBus,
            logger)
    {
        Kernel.Plugins.AddFromObject(new OrderSystemPlugin(orderSystem), "OrderSystem");
    }

    protected override string GetModelId() => "gpt-4o";

    protected override void ConfigurePlugins(Kernel kernel)
    {
        kernel.Plugins.AddFromObject(new AgentBusPlugin(AgentBus, AgentId), "AgentBus");
    }
}
