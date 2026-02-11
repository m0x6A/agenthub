# Autonomous AgentBus Examples

## Why These Are TRULY Autonomous Agents (Not Microservices)

### The Key Difference

**❌ Microservice Pattern** (What we DON'T have):
```
Event Received → if (eventType == "X") { callServiceY(); publishEventZ(); }
```
- Hardcoded logic
- Deterministic responses
- No reasoning
- Just choreographed services

**✅ Autonomous Agent Pattern** (What we HAVE):
```
Event Received → LLM Analyzes Context → LLM Decides Relevance → 
LLM Chooses Tools → Execute Actions → LLM Determines Next Steps
```
- **Real AI decision-making** using Language Models
- **Dynamic responses** based on context and reasoning
- **Tool selection** - agents choose which capabilities to use
- **Multi-step planning** - agents think through problems
- **Goal-oriented** - working toward objectives, not just reacting

## Architecture

### Autonomous Agent Components

Each agent is built on:

1. **Semantic Kernel** - LLM orchestration framework
2. **Function Calling/Plugins** - Tools agents can choose to use
3. **Chat History** - Conversational memory across events
4. **System Prompt** - Guidelines and objectives (not rules)
5. **AgentBus Integration** - Real inter-agent communication

### Agent Decision Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  EVENT ARRIVES                                                   │
├─────────────────────────────────────────────────────────────────┤
│  Agent receives event envelope with type, source, data           │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│  LLM ANALYSIS PHASE                                              │
├─────────────────────────────────────────────────────────────────┤
│  • LLM reads event context                                       │
│  • Analyzes relevance to agent's role                            │
│  • Understands intent and urgency                                │
│  • Recalls previous conversation history                         │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│  DECISION MAKING PHASE                                           │
├─────────────────────────────────────────────────────────────────┤
│  • LLM decides: "Is this relevant to me?"                        │
│  • LLM determines: "What information do I need?"                 │
│  • LLM chooses: "Which tools should I use?"                      │
│  • LLM plans: "What's my strategy?"                              │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│  TOOL EXECUTION PHASE (Function Calling)                         │
├─────────────────────────────────────────────────────────────────┤
│  • LLM autonomously calls chosen tools                           │
│  • Examples:                                                     │
│    - OrderSystem.get_order_details("ORD-123")                    │
│    - Inventory.check_inventory("SKU-456")                        │
│    - Payment.authorize_payment("CUST-001", 500.00)               │
│  • LLM sees tool results and continues reasoning                 │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│  COORDINATION PHASE                                              │
├─────────────────────────────────────────────────────────────────┤
│  • LLM decides: "Should I communicate with other agents?"        │
│  • If yes, LLM calls: AgentBus.publish_event(...)                │
│  • LLM composes event payload based on findings                  │
│  • Other agents autonomously decide how to respond               │
└─────────────────────────────────────────────────────────────────┘
```

## The Four Autonomous Agents

### 1. Customer Experience Agent
**Model**: GPT-4o (best reasoning for customer interaction)  
**Transport**: HTTP REST API  
**External System**: Mock Order Management (SAP/Salesforce-like)

**Autonomous Behaviors**:
- Analyzes customer inquiries using natural language understanding
- Decides which order information to retrieve based on context
- Determines urgency from sentiment analysis
- Chooses whether to handle directly or coordinate with other agents
- Adapts responses based on customer tone

**Tools Available**:
- `OrderSystem.get_order_details` - Retrieve order information
- `OrderSystem.update_order` - Modify orders
- `OrderSystem.confirm_order_fulfillment` - Final confirmation
- `AgentBus.publish_event` - Request help from other agents

**Example Reasoning**:
```
Customer: "Where's my order? I need it urgently!"

Agent's Internal Reasoning (LLM):
"Customer is concerned and urgent. Need to check order status.
If there's a delay, should coordinate with Operations agent.
Let me get order details first, then decide next steps."

Actions Chosen by LLM:
1. Call get_order_details("ORD-2024-001")
2. Analyze: Status is "processing", needs inventory check
3. Call publish_event("customer.inquiry.analyzed", {...})
   to request Operations agent's help
```

### 2. Operations & Inventory Agent
**Model**: GPT-4o-mini (cost-effective for structured operations)  
**Transport**: Azure Service Bus (with HTTP fallback)  
**External System**: Mock Inventory Database (SQL/CosmosDB-like)

**Autonomous Behaviors**:
- Analyzes inventory requirements from customer needs
- Decides optimal fulfillment strategy (cost vs speed trade-offs)
- Determines when to reserve inventory vs wait for payment
- Chooses between single warehouse or split shipment
- Identifies when procurement is needed

**Tools Available**:
- `Inventory.check_inventory` - Check stock levels
- `Inventory.reserve_inventory` - Reserve products
- `Inventory.determine_fulfillment_strategy` - Optimize shipping
- `AgentBus.publish_event` - Coordinate with Financial agent

**Example Reasoning**:
```
Event: customer.inquiry.analyzed (request for 2 Premium Widgets)

Agent's Internal Reasoning:
"Premium Widgets are high-value items. Need to check inventory
across all warehouses. If available, should I reserve now or
wait for payment auth? Order value looks high ($500+), should
coordinate with Financial agent first for risk assessment."

Actions Chosen by LLM:
1. Call check_inventory("SKU-456")
2. Analyze: 15 available in WH-East-01
3. Call determine_fulfillment_strategy to optimize
4. Decide: High value, defer reservation until payment approved
5. Call publish_event("operations.inventory.checked") with findings
```

### 3. Financial Authorization Agent
**Model**: GPT-4o (strong reasoning for financial decisions)  
**Transport**: HTTP REST API  
**External System**: Mock Payment Gateway (Stripe/PayPal-like)

**Autonomous Behaviors**:
- Analyzes transaction risk using multiple factors
- Decides whether to authorize, decline, or flag for manual review
- Balances fraud prevention with customer experience
- Determines appropriate risk thresholds based on context
- Chooses when to capture payments vs hold authorization

**Tools Available**:
- `Payment.authorize_payment` - Authorize with fraud detection
- `Payment.capture_payment` - Charge authorized payment
- `AgentBus.publish_event` - Communicate decisions

**Example Reasoning**:
```
Event: operations.inventory.checked (order ready, value $850)

Agent's Internal Reasoning:
"High-value order at $850. Need to authorize payment and assess risk.
International customer might have higher fraud indicators, but
shouldn't penalize legitimate customers. Let me authorize and
analyze the risk score."

Actions Chosen by LLM:
1. Call authorize_payment("CUST-001", 850.00)
2. Analyze result: Risk score 35 (MEDIUM), Status approved
3. Reasoning: "Risk is acceptable for this amount, approved"
4. Call publish_event("financial.authorization.completed") with auth code
5. If approved, also publish "order.confirmed" to complete flow
```

### 4. Internal Communications Agent
**Model**: GPT-4o (strong reasoning for communication decisions)  
**Transport**: Azure Service Bus (with HTTP fallback)  
**External System**: Mock Microsoft Teams API  
**Special**: Global event subscription (observes all events)

**Autonomous Behaviors**:
- Monitors ALL system events (global observer)
- Decides which events are significant enough to notify teams
- Chooses appropriate Teams channels based on event type
- Determines priority levels (normal, high, urgent)
- Composes contextual, actionable messages for humans
- Filters noise from signal intelligently

**Tools Available**:
- `Teams.post_to_channel` - Simple notifications
- `Teams.post_adaptive_card` - Rich interactive cards
- `AgentBus.publish_event` - Create audit logs

**Example Reasoning**:
```
Event: financial.authorization.completed ($1,850 order approved)

Agent's Internal Reasoning:
"High-value transaction completed. Finance team should be aware
for fraud monitoring purposes. Not urgent (transaction approved),
but significant amount warrants notification. Should use adaptive
card to provide structured details."

Actions Chosen by LLM:
1. Decide: Yes, notify Finance team
2. Choose: Adaptive card (better for structured data)
3. Call post_adaptive_card with order ID, amount, risk score
4. Priority: normal (informational, not urgent)
5. Call publish_event("audit.log.created") for compliance
```

## Running the Autonomous Agents

### Prerequisites

```bash
# Required: AgentBus Broker
cd src/AgentBus.Broker
dotnet run

# Optional: Real LLM (without this, agents use mock responses)
export OPENAI_API_KEY="sk-..."

# Optional: Azure Service Bus (otherwise HTTP is used)
export SERVICEBUS_CONNECTION_STRING="Endpoint=sb://..."
```

### Start Agents

**Option 1: All at once**
```bash
# PowerShell
.\examples\run-all-agents.ps1

# Bash
./examples/run-all-agents.sh
```

**Option 2: Individual agents**
```bash
dotnet run --project examples/AgentBus.Examples.CustomerExperience
dotnet run --project examples/AgentBus.Examples.OperationsInventory
dotnet run --project examples/AgentBus.Examples.FinancialAuth
dotnet run --project examples/AgentBus.Examples.InternalComms
```

### Watch the Autonomous Behavior

When you run the agents, you'll see:

```
🤖 AUTONOMOUS CUSTOMER EXPERIENCE AGENT - LLM-Powered Decision Making
══════════════════════════════════════════════════════════════════════

✅ Autonomous agent registered
🧠 Mode: AUTONOMOUS - LLM analyzes events and chooses actions
🔧 Tools: OrderSystem (get/update/confirm), AgentBus (publish)
📡 Transport: HTTP REST API
💡 Set OPENAI_API_KEY for real LLM reasoning

👂 Listening for events... (Ctrl+C to stop)

📬 SIMULATING CUSTOMER INQUIRY
─────────────────────────────────────────────────────────────────────

══════════════════════════════════════════════════════════════════════
📩 Agent customer-experience-agent analyzing event: customer.inquiry.received
🧠 Agent customer-experience-agent reasoning about event...
💭 Agent customer-experience-agent decision: I need to check the order status
     to address the customer's urgent concern. Let me retrieve the order details.
     [Calling get_order_details...]
══════════════════════════════════════════════════════════════════════
```

## Key Differences from Microservices

| Aspect | Microservices | Autonomous Agents |
|--------|--------------|-------------------|
| **Decision Logic** | Hardcoded if/then | LLM reasoning |
| **Responses** | Deterministic | Context-dependent |
| **Tool Usage** | Fixed workflow | Agent chooses tools |
| **Coordination** | Choreographed | Emergent collaboration |
| **Adaptability** | Requires code changes | Adapts via prompts |
| **Intelligence** | Rule-based | AI-powered |

## Real Autonomy Checklist

✅ **LLM analyzes each event** - Not just pattern matching  
✅ **Agent decides relevance** - Can ignore irrelevant events  
✅ **Agent chooses tools** - Not prescribed workflows  
✅ **Multi-step reasoning** - Plans and executes strategies  
✅ **Conversational memory** - Maintains context across interactions  
✅ **Goal-oriented** - Works toward objectives  
✅ **Emergent coordination** - Agents collaborate without choreography  
✅ **Adaptable via prompts** - Behavior changes without code changes  

## Technical Implementation

### BasedClass: AutonomousAgent

```csharp
public abstract class AutonomousAgent
{
    protected readonly Kernel Kernel;              // Semantic Kernel
    protected readonly IChatCompletionService Chat; // LLM service
    protected readonly ChatHistory ChatHistory;     // Conversational memory
    
    public async Task ProcessEventAsync(EventEnvelope evt)
    {
        // Present event to LLM for analysis
        ChatHistory.AddUserMessage($"EVENT: {evt.EventType}...");
        
        // LLM reasons and autonomously calls tools
        var response = await Chat.GetChatMessageContentAsync(
            ChatHistory,
            executionSettings: new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            },
            Kernel);
        
        // LLM's decision is executed via function calling
        ChatHistory.AddAssistantMessage(response.Content);
    }
}
```

### Plugins as Tools

```csharp
public class OrderSystemPlugin
{
    [KernelFunction("get_order_details")]
    [Description("Retrieves order information. Use when you need order data.")]
    public async Task<string> GetOrderDetailsAsync(string orderId)
    {
        // LLM decides WHEN to call this, not hardcoded logic
    }
}
```

## Benefits of True Autonomy

1. **Emergence** - Complex behaviors emerge from simple agent interactions
2. **Adaptability** - Change behavior by updating system prompts, not code
3. **Intelligence** - Agents reason about trade-offs and context
4. **Scalability** - Add new agents without choreographing interactions
5. **Resilience** - Agents adapt to unexpected situations
6. **Transparency** - LLM reasoning can be logged and explained

## Next Steps

1. **Add real LLM API key** to see true autonomous reasoning
2. **Modify system prompts** to change agent behavior without code changes
3. **Add new tools/plugins** - agents will discover and use them
4. **Create new agents** - they'll autonomously coordinate
5. **Analyze agent reasoning** - log LLM thought processes

---

**This is not just inter-service communication. This is multi-agent AI systems with real autonomy.**
