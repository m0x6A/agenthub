# Quick Start: Autonomous Agents vs Microservices

## The Critical Distinction

### ❌ What You Asked Me NOT To Build
```
Microservices disguised as agents:
  if (event.type == "customer.inquiry") {
    orderData = orderService.get(orderId);
    publish("inquiry.analyzed", orderData);
  }
```
**This is choreographed, deterministic, hardcoded.**

### ✅ What We Actually Built
```
Autonomous agents with LLM reasoning:
  LLM analyzes event context
  LLM decides: "Is this relevant? What do I need?"
  LLM chooses tools: get_order_details(...), publish_event(...)
  LLM adapts response based on customer sentiment
```
**This is autonomous, intelligent, context-aware.**

## Run the Examples

### 1. Start AgentBus Broker
```bash
cd src/AgentBus.Broker
dotnet run
```

### 2. Set API Key (Optional but Recommended)
```bash
# For REAL autonomous behavior
export OPENAI_API_KEY="sk-..."

# Without this, agents use mock responses
```

### 3. Run All Agents
```bash
# PowerShell
.\examples\run-all-agents.ps1

# Bash
./examples/run-all-agents.sh
```

## See Autonomous Behavior

Watch agents reason:
```
📩 Agent analyzing event: customer.inquiry.received
🧠 Agent reasoning about event...
💭 Agent decision: "Customer is urgent ('I need it now'). 
   Should check order status immediately. If delayed, 
   coordinate with Operations agent."
🔧 Agent chose tools:
   - get_order_details("ORD-2024-001") 
   - publish_event("customer.inquiry.analyzed")
```

## The 4 Autonomous Agents

| Agent | Model | What Makes It Autonomous |
|-------|-------|-------------------------|
| **Customer Experience** | GPT-4o | Analyzes customer sentiment, chooses appropriate tools, adapts tone |
| **Operations & Inventory** | GPT-4o-mini | Optimizes fulfillment strategy, balances cost vs speed |
| **Financial Authorization** | GPT-4o | Assesses risk intelligently, balances fraud prevention vs UX |
| **Internal Communications** | GPT-4o | Decides what's worth notifying, filters signal from noise |

## Real Autonomy Proof Points

✅ **LLM makes decisions** - Not if/then logic  
✅ **Agents choose tools** - Function calling, not workflows  
✅ **Context-aware** - Same event, different responses based on context  
✅ **Multi-step reasoning** - "First check X, then based on result, do Y or Z"  
✅ **Goal-oriented** - Working toward objectives, not just reacting  
✅ **Emergent coordination** - Agents collaborate without choreography  
✅ **Conversational memory** - Remember previous interactions  
✅ **Prompt-driven behavior** - Change behavior by updating system prompts  

## Architecture Comparison

### Microservice (What This Is NOT):
```
Customer Service → [if customer.inquiry] → Get Order → Publish Result
    └── Hardcoded flow, deterministic, dumb
```

### Autonomous Agent (What This IS):
```
Customer Service → LLM Analyzes → Decides Relevance → Chooses Tools → 
Executes → Evaluates → Decides Next Action → Publishes (if needed)
    └── AI-driven, context-aware, intelligent
```

## Key Files

- `AutonomousAgent.cs` - Base class with LLM decision-making
- `Plugins/*.cs` - Tools agents can choose to use
- `*Agent/Program.cs` - Each agent's system prompt and capabilities

## Validate True Autonomy

Run agents and verify:

1. **Not all events trigger actions** - Agents ignore irrelevant events
2. **Different responses to similar events** - Based on context
3. **Tool selection varies** - Agents choose different tool combinations
4. **Reasoning is visible** - See LLM thought process in logs
5. **Emergent coordination** - Agents collaborate without explicit choreography

---

**This is multi-agent AI, not microservices wearing agent costumes.**
