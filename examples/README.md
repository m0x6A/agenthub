# AgentBus Example Agents - Real Multi-Agent System

This directory demonstrates **production-ready multi-agent orchestration** with real AgentBus communication, mixed transport protocols, and mock external system integrations.

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        AGENTBUS BROKER                               │
│                (HTTP REST API + Service Bus Topics)                  │
└────┬────────────┬─────────────────┬────────────────┬────────────────┘
     │ HTTP       │ Service Bus     │ HTTP           │ Service Bus
     │            │                 │                │
┌────▼──────┐ ┌──▼───────────┐ ┌───▼─────────┐ ┌───▼──────────────┐
│ Customer  │ │  Operations  │ │  Financial  │ │    Internal      │
│   Agent   │ │  & Inventory │ │Authorization│ │ Communications   │
│  (HTTP)   │ │ (ServiceBus) │ │   (HTTP)    │ │  (ServiceBus)    │
└────┬──────┘ └──┬───────────┘ └───┬─────────┘ └───┬──────────────┘
     │           │                  │                │
 ┌───▼─────┐ ┌──▼────────┐   ┌─────▼──────┐   ┌────▼──────┐
 │ Order   │ │ Inventory │   │  Payment   │   │ Microsoft │
 │ System  │ │ Database  │   │  Gateway   │   │   Teams   │
 │ (Mock)  │ │  (Mock)   │   │   (Mock)   │   │  (Mock)   │
 └─────────┘ └───────────┘   └────────────┘   └───────────┘
```

## 🤖 Agent Details

### 1. Customer Experience Agent
**File**: [AgentBus.Examples.CustomerExperience/Program.cs](AgentBus.Examples.CustomerExperience/Program.cs)

- **Transport**: HTTP (REST API)
- **External System**: Order Management System API
- **LLM Model**: GPT-4o (OpenAI)
- **Responsibilities**:
  - Receives customer inquiries
  - Queries order data from external Order System
  - Publishes `customer.inquiry.received` events to AgentBus
  - Listens for `order.confirmed` events
  - Generates customer-facing responses

**External System Integration**:
```csharp
var orderSystem = new MockOrderSystemApi();
var order = await orderSystem.GetOrderAsync("ORD-2024-001");
```

### 2. Operations & Inventory Agent
**File**: [AgentBus.Examples.OperationsInventory/Program.cs](AgentBus.Examples.OperationsInventory/Program.cs)

- **Transport**: Azure Service Bus (falls back to HTTP)
- **External System**: Inventory Database
- **LLM Model**: GPT-4o-mini (cost-effective for operations)
- **Responsibilities**:
  - Checks inventory levels in database
  - Reserves stock for orders
  - Determines optimal fulfillment warehouse
  - Calculates shipping estimates
  - Publishes `operations.inventory.checked` events

**External System Integration**:
```csharp
var inventoryDb = new MockInventoryDatabase();
var item = await inventoryDb.CheckInventoryAsync("SKU-789");
var reservationId = await inventoryDb.ReserveInventoryAsync("SKU-789", 2);
var fulfillment = await inventoryDb.DetermineFulfillmentAsync(skus, zipCode);
```

### 3. Financial Authorization Agent
**File**: [AgentBus.Examples.FinancialAuth/Program.cs](AgentBus.Examples.FinancialAuth/Program.cs)

- **Transport**: HTTP
- **External System**: Payment Gateway
- **LLM Model**: Claude 3.5 Sonnet (conceptual - superior financial reasoning)
- **Responsibilities**:
  - Calculates order totals (subtotal + tax + shipping)
  - Performs risk assessment
  - Authorizes payments via gateway
  - Publishes `financial.authorization.completed` events
  - Publishes `order.confirmed` events

**External System Integration**:
```csharp
var paymentGateway = new MockPaymentGateway();
var authResult = await paymentGateway.AuthorizePaymentAsync(customerId, amount);
```

### 4. Internal Communications Agent
**File**: [AgentBus.Examples.InternalComms/Program.cs](AgentBus.Examples.InternalComms/Program.cs)

- **Transport**: Azure Service Bus (falls back to HTTP)
- **External System**: Microsoft Teams API
- **LLM Model**: GPT-4o
- **Responsibilities**:
  - Subscribes to ALL events (audit trail)
  - Routes notifications to appropriate Teams channels
  - Posts to #operations-alerts, #finance-team, #customer-service
  - Generates management dashboard updates
  - Creates audit logs

**External System Integration**:
```csharp
var teamsApi = new MockTeamsApi();
await teamsApi.PostToChannelAsync("operations-alerts", title, message, priority);
await teamsApi.PostAdaptiveCardAsync("management-dashboard", title, fields);
```

## 🔄 Complete Event Flow

Here's what happens when you run all 4 agents:

```
T+0s:  Customer Agent starts, simulates inquiry:
       "Add 2x Premium Widget Plus (SKU-789) to order ORD-2024-001"
       
T+0s:  Customer Agent → Order System API
       GET /orders/ORD-2024-001
       Response: Current order with 3x Premium Widget ($171.95 total)
       
T+0s:  Customer Agent → AgentBus
       PUBLISH customer.inquiry.received
       { orderId: "ORD-2024-001", requestedSku: "SKU-789", quantity: 2 }

T+3s:  Operations Agent (via Service Bus) ← AgentBus
       RECEIVE customer.inquiry.received
       
T+3s:  Operations Agent → Inventory Database
       SELECT * FROM inventory WHERE sku = 'SKU-789'
       Response: 47 units available, $24.99/unit, Warehouse WH-East-01
       
T+3s:  Operations Agent → Inventory Database
       RESERVE 2 units of SKU-789
       Response: Reservation RES-ABC123
       
T+3s:  Operations Agent → Inventory Database
       CALCULATE fulfillment for ZIP 98101
       Response: Ship from WH-East-01, ETA 1 day, $9.99 shipping
       
T+3s:  Operations Agent → AgentBus
       PUBLISH operations.inventory.checked
       { available: true, sku: "SKU-789", quantity: 2, unitPrice: 24.99,
         warehouseLocation: "WH-East-01", reservationId: "RES-ABC123" }

T+6s:  Financial Agent ← AgentBus
       RECEIVE operations.inventory.checked
       
T+6s:  Financial Agent calculates:
       Subtotal: 2 × $24.99 = $49.98
       Tax (8%): $3.99
       Shipping: $9.99
       Total: $63.96
       
T+6s:  Financial Agent → Payment Gateway
       POST /authorize { customerId: "C12345", amount: 63.96 }
       Response: { transactionId: "TXN-20240211-ABCD", 
                   authCode: "AUTH-XYZ123", status: "approved", riskScore: 12 }
       
T+6s:  Financial Agent → AgentBus
       PUBLISH financial.authorization.completed
       { transactionId: "TXN-20240211-ABCD", status: "approved",
         amount: 63.96, riskScore: 12 }
       
T+6s:  Financial Agent → AgentBus
       PUBLISH order.confirmed
       { orderId: "ORD-2024-001", finalAmount: 63.96,
         paymentAuthCode: "AUTH-XYZ123" }

T+6s:  Customer Agent ← AgentBus
       RECEIVE order.confirmed
       Generates response: "Great news! Your order update is confirmed..."
       
T+9s:  Internal Comms Agent (via Service Bus) ← AgentBus
       RECEIVE ALL EVENTS (observability mode)
       
T+9s:  Internal Comms Agent processes each event:
       
       customer.inquiry.received →
         Teams API: POST #customer-service
         "New Customer Inquiry: Order ORD-2024-001, SKU: SKU-789, Qty: 2"
       
       operations.inventory.checked →
         Teams API: POST #operations-alerts
         "Inventory Reserved: 2x SKU-789, Warehouse: WH-East-01, ETA: 1 day"
       
       financial.authorization.completed →
         Teams API: POST #finance-team
         "Payment Authorization APPROVED: $63.96, Transaction: TXN-..., Risk: 12/100"
       
       order.confirmed →
         Teams API: POST #customer-service
         "Order Confirmed: ORD-2024-001, Total: $63.96"
         
         Teams API: POST #management-dashboard (Adaptive Card)
         "Transaction Flow Completed: 3 agents, < 1 minute, Status: ✅"
         
T+9s:  Internal Comms Agent → AgentBus
       PUBLISH audit.log.created
       { eventCount: 5, summary: "..." }
```

## 📦 Mock External Systems

All external integrations are mocked to demonstrate production patterns:

### MockOrderSystemApi.cs
Simulates enterprise order management systems (SAP, Salesforce, custom ERP):
- `GetOrderAsync()` - Retrieve order details
- `UpdateOrderAsync()` - Add items to order
- `ConfirmOrderAsync()` - Mark order as confirmed
- Includes realistic API latency (100-150ms)

### MockInventoryDatabase.cs
Simulates inventory data stores (SQL Server, PostgreSQL, CosmosDB):
- `CheckInventoryAsync()` - Query stock levels
- `ReserveInventoryAsync()` - Lock inventory
- `DetermineFulfillmentAsync()` - Calculate optimal warehouse and shipping
- Realistic data with SKUs, quantities, warehouses

### MockPaymentGateway.cs
Simulates payment processors (Stripe, PayPal, Authorize.net):
- `AuthorizePaymentAsync()` - Authorize payment with risk scoring
- `CapturePaymentAsync()` - Capture authorized payment
- Transaction management with IDs and auth codes
- Fraud detection simulation

### MockTeamsApi.cs
Simulates Microsoft Graph API / Teams webhooks:
- `PostToChannelAsync()` - Send messages to Teams channels
- `PostAdaptiveCardAsync()` - Send rich formatted cards
- Visual console output showing Teams notifications
- Channel routing by department

## 🚀 Running the Examples

### Prerequisites

1. **Start AgentBus Broker** (required):
   ```bash
   cd src/AgentBus.Broker
   dotnet run
   ```
   The broker must be running on `http://localhost:5000`

2. **(Optional) Configure Service Bus**:
   ```bash
   export SERVICEBUS_CONNECTION_STRING="Endpoint=sb://your-namespace.servicebus.windows.net/;..."
   ```
   Without this, agents use HTTP transport (works perfectly fine for demo)

3. **(Optional) Configure LLM APIs**:
   ```bash
   export OPENAI_API_KEY="sk-..."
   export ANTHROPIC_API_KEY="sk-ant-..."
   ```
   Without these, agents use mock responses (still demonstrates architecture)

### Option 1: Launch All Agents Together

**PowerShell (Windows):**
```powershell
.\examples\run-all-agents.ps1
```

**Bash (Linux/Mac):**
```bash
chmod +x examples/run-all-agents.sh
./examples/run-all-agents.sh
```

This starts all 4 agents in separate terminal windows with staggered delays.

### Option 2: Run Individual Agents

Open 4 separate terminals:

**Terminal 1 - Customer Experience:**
```bash
cd examples/AgentBus.Examples.CustomerExperience
dotnet run
```

**Terminal 2 - Operations & Inventory:**
```bash
cd examples/AgentBus.Examples.OperationsInventory
dotnet run
```

**Terminal 3 - Financial Authorization:**
```bash
cd examples/AgentBus.Examples.FinancialAuth
dotnet run
```

**Terminal 4 - Internal Communications:**
```bash
cd examples/AgentBus.Examples.InternalComms
dotnet run
```

## 📊 What You'll See

Each agent displays:

1. **Initialization**:
   ```
   ═══════════════════════════════════════════════════════════
   🎯 CUSTOMER EXPERIENCE AGENT - E-COMMERCE PLATFORM
   ═══════════════════════════════════════════════════════════
   Transport: HTTP
   External System: Order Management System (Mock)
   Model: gpt-4o
   ```

2. **Registration**:
   ```
   [12:34:56 INF] 📝 Registering with AgentBus...
   [12:34:56 INF] [HTTP] Agent registered: customer-experience-agent
   [12:34:56 INF] ✅ Registered!
   ```

3. **Subscription**:
   ```
   [12:34:56 INF] 🔔 Subscribing to global events...
   [12:34:56 INF] [HTTP] Subscribed to global events: sub-customer-experience-agent-global
   [12:34:56 INF] ✅ Subscribed: sub-customer-experience-agent-global
   ```

4. **External System Interactions**:
   ```
   📦 Current Order: ORD-2024-001 | Total: $171.95 | Items: 1
   ```

5. **Event Publishing**:
   ```
   ✅ Published: customer.inquiry.received
   ```

6. **Event Receiving**:
   ```
   📨 order.confirmed from financial-authorization-agent at 12:35:02
   🎉 Order Confirmed!
   ```

7. **Teams Notifications** (Internal Comms Agent):
   ```
   ═══════════════════════════════════════════════════════════
   📱 MICROSOFT TEAMS - #operations-alerts
   ═══════════════════════════════════════════════════════════
   ✅ Inventory Reserved
   
   Reserved 2x SKU-789
   Warehouse: WH-East-01
   Ship ETA: 1 days
   
   🕐 12:35:01
   ═══════════════════════════════════════════════════════════
   ```

## 🎯 Key Features Demonstrated

### 1. Real AgentBus Communication
- Actual HTTP requests to AgentBus broker
- Real Service Bus message delivery (when configured)
- Not mocked - genuine distributed communication

### 2. Mixed Transport Protocols
- Customer Agent: HTTP REST API
- Operations Agent: Service Bus → HTTP fallback
- Financial Agent: HTTP REST API
- Internal Comms Agent: Service Bus → HTTP fallback

### 3. External System Integration Patterns
- Mock systems with realistic interfaces
- Latency simulation
- Error handling
- Production-ready patterns

### 4. Event-Driven Architecture
- Publish/Subscribe pattern
- Global event subscription (audit)
- Correlation IDs
- Event sourcing ready

### 5. Observability
- Structured logging (Serilog)
- Console output for visibility
- Cross-cutting concerns (Internal Comms)
- Audit trail

## 🧪 Testing the System

### Verify AgentBus Connection
If agents can't connect, you'll see:
```
❌ Cannot connect to AgentBus at http://localhost:5000
💡 Start broker: cd src/AgentBus.Broker && dotnet run
```

### Verify External System Mocks
Watch for log entries like:
```
[12:35:00 INF] [OrderSystem API] Retrieved order: ORD-2024-001
[12:35:01 INF] [Inventory DB] Checked SKU-789: 47 available
[12:35:02 INF] [Payment Gateway] Authorization approved: TXN-...
```

### Verify Event Flow
Each agent should show received events from other agents via AgentBus.

## 📁 Code Structure

```
examples/
├── AgentBus.Examples.Shared/                 # Shared library
│   ├── AgentBus.Examples.Shared.csproj
│   ├── IAgentBusClient.cs                    # Client interface
│   ├── HttpAgentBusClient.cs                 # HTTP implementation
│   ├── ServiceBusAgentBusClient.cs           # Service Bus implementation
│   ├── Models.cs                              # Shared data models
│   └── ExternalSystems/
│       ├── MockOrderSystemApi.cs
│       ├── MockInventoryDatabase.cs
│       ├── MockPaymentGateway.cs
│       └── MockTeamsApi.cs
│
├── AgentBus.Examples.CustomerExperience/
│   ├── AgentBus.Examples.CustomerExperience.csproj
│   └── Program.cs                             # HTTP + Order System integration
│
├── AgentBus.Examples.OperationsInventory/
│   ├── AgentBus.Examples.OperationsInventory.csproj
│   └── Program.cs                             # Service Bus + Inventory DB
│
├── AgentBus.Examples.FinancialAuth/
│   ├── AgentBus.Examples.FinancialAuth.csproj
│   └── Program.cs                             # HTTP + Payment Gateway
│
├── AgentBus.Examples.InternalComms/
│   ├── AgentBus.Examples.InternalComms.csproj
│   └── Program.cs                             # Service Bus + Teams API
│
├── run-all-agents.ps1
├── run-all-agents.sh
├── README.md                                  # This file
└── QUICKSTART.md                               # Quick reference
```

## 💡 Learning Objectives

These examples teach:

1. **Multi-Agent System Design**
   - Agent autonomy and specialization
   - Loose coupling via events
   - No direct agent-to-agent calls

2. **Transport Protocol Selection**
   - HTTP for synchronous request/response
   - Service Bus for reliable async messaging
   - Seamless fallback mechanisms

3. **External System Integration**
   - Abstraction of external dependencies
   - Mock implementations for development
   - Production-ready interfaces

4. **Event-Driven Architecture**
   - Pub/Sub patterns
   - Event types and versioning
   - Global observability via subscription

5. **Observability and Monitoring**
   - Structured logging
   - Cross-cutting concerns
   - Audit trails

6. **Production Patterns**
   - Error handling and retries
   - Configuration management
   - Graceful degradation

## 🛠️ Extending the Examples

### Add a New Agent

1. Create new console project:
   ```bash
   dotnet new console -n AgentBus.Examples.YourAgent
   ```

2. Reference shared library:
   ```xml
   <ProjectReference Include="..\AgentBus.Examples.Shared\AgentBus.Examples.Shared.csproj" />
   ```

3. Choose transport:
   ```csharp
   // HTTP
   var client = new HttpAgentBusClient(url, agentId);
   
   // Service Bus
   var client = new ServiceBusAgentBusClient(url, connStr, agentId);
   ```

4. Register and subscribe:
   ```csharp
   await client.RegisterAgentAsync(registration);
   var subscription = await client.SubscribeToAllEventsAsync();
   ```

5. Handle events and publish responses

### Add a New Mock External System

1. Create interface in `AgentBus.Examples.Shared/ExternalSystems/`:
   ```csharp
   public class MockYourSystemApi
   {
       public async Task<Data> QueryAsync(string id)
       {
           await Task.Delay(100); // Simulate latency
           return mockData;
       }
   }
   ```

2. Use in agent:
   ```csharp
   var externalSystem = new MockYourSystemApi();
   var data = await externalSystem.QueryAsync("123");
   ```

### Replace Mocks with Real Systems

Replace mock constructors with real implementations:

```csharp
// Mock
var orderSystem = new MockOrderSystemApi();

// Real
var orderSystem = new RealOrderSystemApi(apiKey, baseUrl);
```

The agent code remains unchanged!

## 🚀 Production Deployment

To move to production:

### 1. Replace Mock External Systems
```csharp
// Replace mocks
var orderSystem = new SapOrderApi(config);
var inventoryDb = new CosmosDbInventoryRepository(client);
var paymentGateway = new StripePaymentService(apiKey);
var teamsApi = new MicrosoftGraphTeamsClient(credential);
```

### 2. Configure Azure Service Bus
```bash
# In Azure Portal, create Service Bus namespace
# Copy connection string
export SERVICEBUS_CONNECTION_STRING="Endpoint=sb://..."
```

### 3. Add Authentication
```csharp
// Use Managed Identity
var credential = new DefaultAzureCredential();
var client = new HttpAgentBusClient(url, agentId, credential);
```

### 4. Deploy as Containers
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
COPY --from=build /app .
ENTRYPOINT ["dotnet", "AgentBus.Examples.CustomerExperience.dll"]
```

### 5. Configure Application Insights
```csharp
builder.Services.AddApplicationInsightsTelemetry();
```

### 6. Set Up Auto-Scaling
```bash
az containerapp update --name customer-agent --min-replicas 1 --max-replicas 10
```

## 📚 Related Documentation

- [AgentBus Architecture](../docs/architecture/ARCHITECTURE.md)
- [API Documentation](../specs/001-deployable-mvp/contracts/openapi.yaml)
- [Deployment Guide](../docs/DEPLOYMENT.md)
- [Testing Guide](../docs/TESTING_GUIDE.md)

## 🐛 Troubleshooting

### Agents can't connect to AgentBus
- Ensure broker is running: `cd src/AgentBus.Broker && dotnet run`
- Check URL: `http://localhost:5000`
- Verify no firewall blocking

### Service Bus not working
- Check connection string is valid
- Verify namespace exists in Azure
- Check agents show "Transport: Azure Service Bus"
- Fallback to HTTP is automatic if Service Bus unavailable

### No events received
- Verify all agents are registered
- Check subscription succeeded
- Look for errors in broker logs
- Ensure event types match

### Build errors
- Run `dotnet restore` in examples directory
- Ensure .NET 9.0 SDK installed
- Check all project references

---

**🎉 You now have a complete, working multi-agent system demonstrating real AgentBus orchestration with production-ready patterns!**
