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
Console.WriteLine("🤖 AUTONOMOUS INTERNAL COMMUNICATIONS AGENT - LLM-Powered Observability");
Console.WriteLine(new string('═', 100) + "\n");

try
{
    var agentId = "internal-comms-agent";
    var brokerUrl = Environment.GetEnvironmentVariable("AGENTBUS_URL") ?? "http://localhost:5000";
    var serviceBusConn = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING");

    IAgentBusClient agentBus;
    if (!string.IsNullOrEmpty(serviceBusConn))
    {
        agentBus = new ServiceBusAgentBusClient(brokerUrl, serviceBusConn, agentId);
        Log.Information("📡 Transport: Azure Service Bus");
    }
    else
    {
        agentBus = new HttpAgentBusClient(brokerUrl, agentId);
        Log.Information("📡 Transport: HTTP");
    }

    var teamsApi = new MockTeamsApi();

    var registration = new AgentRegistration(
        Id: agentId,
        Name: "Autonomous Internal Communications Agent",
        Version: "2.0.0",
        Capabilities: new[] { "autonomous-reasoning", "observability", "intelligent-alerting" },
        MessageTypes: new MessageTypes(new[] { "*.*" }, new[] { "audit.log.created" }),
        Communication: new CommunicationCapabilities(false, true, true),
        EventsPublished: new[] { "audit.log.created" },
        Metadata: new AgentMetadata("demo", "dev", new[] { "autonomous", "global-observer" }));

    await agentBus.RegisterAgentAsync(registration);
    var subscription = await agentBus.SubscribeToAllEventsAsync();

    var agent = new InternalCommunicationsAgent(agentId, agentBus, teamsApi, Log.Logger);

    Log.Information("✅ Autonomous agent registered");
    Log.Information("🧠 Mode: AUTONOMOUS - LLM decides what's worth communicating");
    Log.Information("🔧 Tools: Microsoft Teams, AgentBus");
    Log.Information("👁️  Observability: Monitoring ALL system events");
    Log.Information("💡 Set OPENAI_API_KEY for real LLM reasoning\n");

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    Log.Information("👂 Listening to ALL events globally...\n");

    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            var eventEnvelope = await agentBus.ReceiveEventAsync(subscription.SubscriptionId, 30, cts.Token);
            if (eventEnvelope == null) continue;

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

class InternalCommunicationsAgent : AutonomousAgent
{
    public InternalCommunicationsAgent(
        string agentId,
        IAgentBusClient agentBus,
        MockTeamsApi teamsApi,
        ILogger logger)
        : base(
            agentId,
            systemPrompt: @"You are an AUTONOMOUS Internal Communications Agent - the INTELLIGENT observer.

YOUR ROLE: Monitor ALL events and DECIDE what humans need to know. Filter signal from noise.

TOOLS:
- Teams.post_to_channel: Simple notification
- Teams.post_adaptive_card: Rich interactive card
- AgentBus.publish_event: Create audit logs

AUTONOMOUS INTELLIGENCE:
For EVERY event:
1. ANALYZE: Is this significant? Does it affect operations?
2. DECIDE: Should teams be notified? Which channel? What priority?
3. COMPOSE: If yes, craft clear, actionable message
4. SELECT TOOL: Simple post vs adaptive card based on importance

NOTIFICATION DECISION GUIDE:
- Customer inquiries → #Customer-Service (Info) - teams should know
- Inventory issues → #Operations (Warning if low)
- Payment issues → #Finance (Critical)
- High-value transactions (>$1000) → #Finance (Info with details)
- Order confirmations → Maybe skip notification, just audit log
- Errors/exceptions → #Engineering (Critical)

PRIORITY LEVELS:
- normal: Business as usual
- high: Needs attention soon
- urgent: Immediate action required

EXAMPLE - financial.authorization.completed for $1850:
  Reasoning: ""High-value transaction. Finance should know for monitoring. Not urgent, but significant.""
  Actions: 1) post_adaptive_card to 'Finance' with order details, amount, risk score
           2) publish_event 'audit.log.created' for compliance

You are the SMART filter. Don't spam teams. Highlight what matters.",
            agentBus,
            logger)
    {
        Kernel.Plugins.AddFromObject(new TeamsPlugin(teamsApi), "Teams");
    }

    protected override string GetModelId() => "gpt-4o";

    protected override void ConfigurePlugins(Kernel kernel)
    {
        kernel.Plugins.AddFromObject(new AgentBusPlugin(AgentBus, AgentId), "AgentBus");
    }
}
