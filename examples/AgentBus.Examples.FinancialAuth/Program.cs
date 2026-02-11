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
Console.WriteLine("🤖 AUTONOMOUS FINANCIAL AUTHORIZATION AGENT - LLM-Powered Risk Assessment");
Console.WriteLine(new string('═', 100) + "\n");

try
{
    var agentId = "financial-authorization-agent";
    var brokerUrl = Environment.GetEnvironmentVariable("AGENTBUS_URL") ?? "http://localhost:5000";

    using var agentBus = new HttpAgentBusClient(brokerUrl, agentId);
    var paymentGateway = new MockPaymentGateway();

    var registration = new AgentRegistration(
        Id: agentId,
        Name: "Autonomous Financial Authorization Agent",
        Version: "2.0.0",
        Capabilities: new[] { "autonomous-reasoning", "risk-assessment", "payment-auth" },
        MessageTypes: new MessageTypes(new[] { "operations.inventory.checked" }, new[] { "financial.authorization.completed", "order.confirmed" }),
        Communication: new CommunicationCapabilities(false, true, true),
        EventsPublished: new[] { "financial.authorization.completed", "order.confirmed" },
        Metadata: new AgentMetadata("demo", "dev", new[] { "autonomous", "financial" }));

    await agentBus.RegisterAgentAsync(registration);
    var subscription = await agentBus.SubscribeToAllEventsAsync();

    var agent = new FinancialAuthorizationAgent(agentId, agentBus, paymentGateway, Log.Logger);

    Log.Information("✅ Autonomous agent registered");
    Log.Information("🧠 Mode: AUTONOMOUS - LLM makes authorization decisions");
    Log.Information("🔧 Tools: Payment Gateway (authorize/capture), AgentBus");
    Log.Information("💡 Set OPENAI_API_KEY for real LLM reasoning\n");

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    Log.Information("👂 Listening for events...\n");

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
}
catch (Exception ex)
{
    Log.Fatal(ex, "Fatal error");
}
finally
{
    await Log.CloseAndFlushAsync();
}

class FinancialAuthorizationAgent : AutonomousAgent
{
    public FinancialAuthorizationAgent(
        string agentId,
        IAgentBusClient agentBus,
        MockPaymentGateway paymentGateway,
        ILogger logger)
        : base(
            agentId,
            systemPrompt: @"You are an AUTONOMOUS Financial Agent that DECIDES on payment authorizations.

YOUR ROLE: Authorize payments by REASONING about fraud risk, not by fixed rules.

TOOLS:
- Payment.authorize_payment: Authorize with fraud detection
- Payment.capture_payment: Charge authorized payment
- AgentBus.publish_event: Communicate decisions

AUTONOMOUS DECISIONS:
When inventory is checked for an order:
1. ANALYZE: Transaction amount, customer ID, risk factors
2. DECIDE: Authorize now? What's acceptable risk level?
3. EXECUTE: Call authorize_payment
4. EVALUATE: Risk score result - approve, decline, or manual review?
5. COORDINATE: If approved, publish confirmation; if high risk, flag for review

RISK ASSESSMENT GUIDELINES:
- Risk < 30: Auto-approve (fast customer experience)
- Risk 30-70: Careful analysis - balance security vs friction
- Risk > 70: Likely decline or manual review
- High amounts (>$1000): Extra scrutiny

EXAMPLE - $850 order, international:
  Reasoning: ""High-value order. International has some risk. Check fraud score. 
              If score is low-medium (<50), approve. Customer shouldn't wait if legitimate.""
  Actions: 1) authorize_payment, 2) Analyze risk score, 3) If approved, publish 'financial.authorization.completed' with auth code

You balance FRAUD PREVENTION with CUSTOMER EXPERIENCE. Be smart, not paranoid.",
            agentBus,
            logger)
    {
        Kernel.Plugins.AddFromObject(new PaymentPlugin(paymentGateway), "Payment");
    }

    protected override string GetModelId() => "gpt-4o";

    protected override void ConfigurePlugins(Kernel kernel)
    {
        kernel.Plugins.AddFromObject(new AgentBusPlugin(AgentBus, AgentId), "AgentBus");
    }
}
