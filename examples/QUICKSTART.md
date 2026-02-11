# AgentBus Example Agents - Real Multi-Agent Orchestration

## 🎯 Overview

These examples demonstrate **production-ready multi-agent collaboration** with:
- ✅ **Real inter-agent communication** through AgentBus (not mocked)
- ✅ **Mixed transport protocols** (HTTP + Azure Service Bus)
- ✅ **Mock external system integrations** (Order System, Inventory DB, Payment Gateway, Teams)
- ✅ **Semantic Kernel** with multiple LLM models
- ✅ **Event-driven architecture** with global observability

## 🚀 Quick Start

### 1. Start AgentBus Broker
```bash
cd src/AgentBus.Broker
dotnet run
```

### 2. Run All Agents
```powershell
.\examples\run-all-agents.ps1
```

### 3. Watch the Magic
Observe agents communicating through AgentBus, querying external systems, and collaborating to process a customer order.

## 🏗️ Architecture

```
Customer Inquiry → [Customer Agent] → Order System API (mock)
                          ↓ (HTTP)
                    [AgentBus Broker]
                          ↓ (Service Bus if configured)
                 [Operations Agent] → Inventory DB (mock)
                          ↓ (HTTP)
                    [AgentBus Broker]
                          ↓ (HTTP)
                  [Financial Agent] → Payment Gateway (mock)
                          ↓ (HTTP)
                    [AgentBus Broker]
                          ↓ (Service Bus)
              [Internal Comms Agent] → Teams API (mock)
```

## 🤖 The Agents

| Agent | Transport | External System | LLM |
|-------|-----------|-----------------|-----|
| Customer Experience | HTTP | Order Management API | GPT-4o |
| Operations & Inventory | Service Bus* | Inventory Database | GPT-4o-mini |
| Financial Authorization | HTTP | Payment Gateway | Claude 3.5 Sonnet |
| Internal Communications | Service Bus* | Microsoft Teams | GPT-4o |

*Falls back to HTTP if Service Bus not configured

## 📦 Mock External Systems

All agents integrate with realistic mock external systems:

- **MockOrderSystemApi** - Simulates SAP/Salesforce order management
- **MockInventoryDatabase** - Simulates SQL/CosmosDB inventory data
- **MockPaymentGateway** - Simulates Stripe/PayPal payment processing
- **MockTeamsApi** - Simulates Microsoft Graph/Teams webhooks

These mocks provide realistic latency, data, and behavior without requiring actual services.

## 🔄 Event Flow Example

1. **Customer**: "Add 2x SKU-789 to order ORD-2024-001"
2. **Customer Agent**: Queries order system → publishes `customer.inquiry.received`
3. **Operations Agent**: Checks inventory DB → reserves stock → publishes `operations.inventory.checked`
4. **Financial Agent**: Calculates pricing → authorizes payment → publishes `order.confirmed`
5. **Customer Agent**: Receives confirmation → responds to customer
6. **Internal Comms**: Posts to Teams channels → creates audit log

## 🎮 Running the Examples

### Individual Agents

```bash
# Terminal 1 - Customer Experience (HTTP)
cd examples/AgentBus.Examples.CustomerExperience
dotnet run

# Terminal 2 - Operations (Service Bus)
cd examples/AgentBus.Examples.OperationsInventory
dotnet run

# Terminal 3 - Financial (HTTP)
cd examples/AgentBus.Examples.FinancialAuth
dotnet run

# Terminal 4 - Internal Comms (Service Bus)
cd examples/AgentBus.Examples.InternalComms
dotnet run
```

### With Service Bus

```bash
export SERVICEBUS_CONNECTION_STRING="Endpoint=sb://..."
```

Without this, agents fall back to HTTP transport seamlessly.

### With Real LLMs

```bash
export OPENAI_API_KEY="sk-..."
export ANTHROPIC_API_KEY="sk-ant-..."
```

Without keys, agents use mock responses.

## 📊 What You'll See

Each agent shows:
- Registration with AgentBus
- External system queries (DB, APIs)
- Events sent/received
- Processing logic
- Teams notifications (Internal Comms agent)
- Complete audit trail

## 📁 Code Structure

```
examples/
├── AgentBus.Examples.Shared/          # Shared infrastructure
│   ├── HttpAgentBusClient.cs          # HTTP transport implementation
│   ├── ServiceBusAgentBusClient.cs    # Service Bus implementation
│   └── ExternalSystems/               # Mock integrations
├── AgentBus.Examples.CustomerExperience/
├── AgentBus.Examples.OperationsInventory/
├── AgentBus.Examples.FinancialAuth/
└── AgentBus.Examples.InternalComms/
```

## 💡 Key Learnings

1. **Transport Abstraction** - Same `IAgentBusClient` interface for HTTP and Service Bus
2. **External System Mocking** - Realistic integration patterns without dependencies
3. **Event-Driven Collaboration** - Agents don't call each other directly
4. **Mixed Protocols** - HTTP and message queues coexist
5. **Observability** - Internal Comms agent monitors everything
6. **Production Patterns** - Error handling, retries, structured logging

## 🔧 Configuration

| Variable | Required | Default | Purpose |
|----------|----------|---------|---------|
| `AGENTBUS_URL` | Yes | `http://localhost:5000` | Broker endpoint |
| `SERVICEBUS_CONNECTION_STRING` | No | - | Enable Service Bus transport |
| `OPENAI_API_KEY` | No | - | Real LLM processing |
| `ANTHROPIC_API_KEY` | No | - | Financial agent LLM |

## 🎓 Educational Value

These examples teach:
- Multi-agent system design
- Event-driven architecture
- Transport protocol selection
- External system integration patterns
- Mock vs. real service boundaries
- Observability and monitoring
- Production deployment considerations

## 🚢 Production Deployment

To productionize:
1. Replace mock external systems with real APIs
2. Configure Azure Service Bus for all agents
3. Deploy as containers to Azure Container Apps
4. Add Azure Key Vault for secrets
5. Enable Application Insights
6. Set up managed identities
7. Configure auto-scaling

---

See full documentation in [examples/README.md](README.md)
