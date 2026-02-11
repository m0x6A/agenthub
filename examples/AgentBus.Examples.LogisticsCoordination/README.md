# Logistics Coordination: Two Agents Solving a Problem Together

## Scenario

Two autonomous agents in the same domain collaborate through natural language to solve a complex logistics problem that neither can solve alone.

**🚚 Shipping Agent**
- Responsible for route planning, carrier selection, shipment optimization
- Cannot fulfill requests without inventory confirmation
- Initiates contact when it needs warehouse help

**📦 Warehouse Lead Agent**
- Responsible for inventory management, bin locations, picking/packing operations  
- Cannot commit inventory without understanding shipping constraints
- Responds to requests and provides inventory insights

## The Problem

An urgent order arrives:
- **Customer**: "I need this shipment expedited to Tokyo within 24 hours"
- **Current situation**: Standard carriers can do it in 48 hours, but express shipping is MUCH more expensive
- **Constraint**: The warehouse only has partial inventory in the local warehouse; rest is in regional hubs

## How They Solve It Together

```
Shipping Agent receives order
  ↓
"I can't decide if this is economically viable without knowing 
 inventory distribution. Let me ask Warehouse Lead..."
  ↓
Sends direct message: "Can you help me with an urgent request?
  We need to ship to Tokyo in 24h. Can we consolidate inventory
  from our regional hubs? What would be the impact?"
  ↓
Warehouse Lead analyzes:
  ✓ Checks inventory across all warehouses
  ✓ Calculates consolidation time (4 hours)
  ✓ Estimates additional labor costs
  ↓
Responds: "Yes, we can do this. Regional consolidation is possible
  and would take 4 hours. Estimated extra labor cost is $250.
  Local inventory is 85% sufficient; need 15% from hub in Osaka."
  ↓
Shipping Agent decides:
  "Given that info, the total cost is viable. I'll proceed with
   the consolidation approach and special carrier."
  ↓
Solution: Both agents execute their parts of the plan
  - Shipping: Books express carrier, creates consolidation shipment
  - Warehouse: Schedules pick/pack operation, coordinates with Osaka hub
```

## Key Differences from Pub/Sub

- **Direct Messages** (not events): They converse back-and-forth, not broadcasting
- **Conversational Context**: Each agent maintains chat history of their conversation
- **Request-Response Pattern**: Agent A asks a specific question, Agent B responds to it
- **Flexible Reasoning**: No hardcoded workflows—agents reason about what to ask and how to proceed
- **Equal Peers**: Neither is "orchestrating" the other; they're collaborating

## Running the Example

```bash
# Start broker
dotnet run --project src/AgentBus.Broker

# In terminal 1: Warehouse Lead Agent (listens for direct messages)
dotnet run --project examples/AgentBus.Examples.LogisticsCoordination WarehouseLead

# In terminal 2: Shipping Agent (initiates the problem-solving conversation)
dotnet run --project examples/AgentBus.Examples.LogisticsCoordination ShippingAgent
```

Watch as they send each other messages asking questions, providing data, and collaborating to find a solution.

## Implementation Details

### Receiving Direct Messages
Each agent listens on its dedicated inbox queue for direct messages from other agents:
```
agentBus.ReceiveDirectMessage(timeout: 30 seconds)
```

### Sending Direct Messages with Natural Language
When an agent needs help, it sends a message to another agent's queue:
```
agentBus.SendDirectMessageAsync(
  to: "warehouse-lead-agent",
  message: "Can you help me with an urgent request..."
)
```

### Chat History Across Messages
Each agent maintains a conversation context, so as messages go back and forth, they remember the context of the discussion.

## Natural Language Flow

The conversation the agents have:

```
SHIPPING AGENT → WAREHOUSE LEAD AGENT:
"Hi! I received an urgent order for Tokyo delivery within 24 hours.
 The customer is a VIP. Standard expedited shipping would cost $3,500.
 
 My question: Can we consolidate inventory from regional hubs
 to make this work? What would be the time and cost impact on your end?
 I need to know if this is economically viable."

WAREHOUSE LEAD AGENT → SHIPPING AGENT:
"Got it. Let me check our inventory distribution...
 
 Good news: We have 85% of the order in local warehouse (Sydney).
 Remaining 15% is in Osaka hub. We can consolidate in 4 hours total.
 
 Cost impact: $250 additional labor + $150 consolidation/handling = $400.
 
 My recommendation: Go for it. Total cost to you: $3,500 + $400 = $3,900.
 That's still reasonable for a VIP express shipment and we can execute quickly."

SHIPPING AGENT → WAREHOUSE LEAD AGENT:
"Perfect, that's within budget. I'm booking the express carrier now.
 
 Action: I'll provide consolidation shipment details in 30 minutes.
 Can you have team ready to pick/pack by 2 PM Sydney time?
 And coordinate with Osaka on the 15% shipment?
 
 Timeline: We need to depart warehouse by 6 PM for airport handoff."

WAREHOUSE LEAD AGENT → SHIPPING AGENT:
"Confirmed. Team will be staged and ready by 2 PM. I've sent
 pull request to Osaka for their 15% portion. They can have it
 ready for overnight delivery to us by 1 PM—will arrive in time.
 
 We're set on our end. Send me those consolidation details
 and customer ID when ready."

SHIPPING AGENT → WAREHOUSE LEAD AGENT:
"Shipment details coming your way via direct message in 2 minutes.
 Customer ID: CUST-VIP-007. Order: ORD-2024-999.
 
 Thanks for the quick turnaround. Appreciate your partnership!"

WAREHOUSE LEAD AGENT → SHIPPING AGENT:
"Always happy to help solve these puzzles together.
 Received your details. We're executing the pick/pack order now.
 Will confirm full shipment ready for collection by 6 PM."
```

This is **true agent collaboration**—neither pre-programmed, both reasoning in natural language.
