# Running with .NET Aspire 🚀

## Overview

This project now includes **.NET Aspire** orchestration, providing a modern cloud-native development experience with:

- **One-Command Start**: Run all 5 projects (broker + 4 agents) simultaneously
- **Aspire Dashboard**: Visual monitoring of all services, logs, traces, and metrics
- **Service Discovery**: Agents automatically discover the broker
- **Distributed Tracing**: See events flow across autonomous agents in real-time
- **Azure Integration**: Provision Service Bus and Application Insights automatically
- **Local Development**: Azure Service Bus emulator for local testing

## Quick Start

### Prerequisites

```bash
# .NET 10 SDK (you have this ✓)
dotnet --version  # Should be 10.x

# Install Aspire workload
dotnet workload install aspire

# Docker Desktop (for Service Bus emulator)
# Download from: https://www.docker.com/products/docker-desktop
```

### Run Everything with Aspire

```bash
# From repository root
dotnet run --project src/AgentHub.AppHost

# This starts:
# 1. Azure Service Bus Emulator (in Docker)
# 2. AgentBus.Broker (localhost:5000)
# 3. Customer Experience Agent
# 4. Operations & Inventory Agent
# 5. Financial Authorization Agent
# 6. Internal Communications Agent
# 7. Aspire Dashboard (https://localhost:17238)
```

### Aspire Dashboard

Once running, open the Aspire Dashboard (URL shown in console):

```
https://localhost:17238
```

**Dashboard Features**:
- **Resources**: See all services status (running, stopped, health)
- **Console Logs**: View logs from all services in one place
- **Structured Logs**: Filter and search across all agents
- **Traces**: Distributed tracing showing event flow
- **Metrics**: Performance metrics for each service

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  Aspire AppHost (Orchestrator)                                │
├──────────────────────────────────────────────────────────────┤
│                                                                │
│  ┌─────────────────┐         ┌──────────────────────┐        │
│  │ Service Bus     │◄────────┤  AgentBus.Broker     │        │
│  │ Emulator        │         │  (localhost:5000)    │        │
│  └─────────────────┘         └──────────────────────┘        │
│           ▲                            ▲                       │
│           │                            │                       │
│           │    ┌───────────────────────┴────────┐             │
│           │    │                                 │             │
│  ┌────────┴────┴───┐     ┌──────────────────────▼──┐         │
│  │ Operations &     │     │  Customer Experience    │         │
│  │ Inventory Agent  │     │  Agent (HTTP)           │         │
│  │ (Service Bus)    │     └─────────────────────────┘         │
│  └──────────────────┘                                          │
│                                                                │
│  ┌──────────────────┐     ┌─────────────────────────┐        │
│  │ Internal Comms   │     │  Financial Auth Agent   │        │
│  │ Agent (SB)       │     │  (HTTP)                 │        │
│  └──────────────────┘     └─────────────────────────┘        │
│                                                                │
│  ┌─────────────────────────────────────────────────┐         │
│  │  Aspire Dashboard (Observability)               │         │
│  │  - Logs  - Traces  - Metrics  - Health         │         │
│  └─────────────────────────────────────────────────┘         │
└──────────────────────────────────────────────────────────────┘
```

## Benefits Over Manual Execution

### Before Aspire (Manual):
```bash
# Terminal 1
cd src/AgentBus.Broker && dotnet run

# Terminal 2
export AGENTBUS_URL=http://localhost:5000
dotnet run --project examples/AgentBus.Examples.CustomerExperience

# Terminal 3
dotnet run --project examples/AgentBus.Examples.OperationsInventory

# Terminal 4
dotnet run --project examples/AgentBus.Examples.FinancialAuth

# Terminal 5
dotnet run --project examples/AgentBus.Examples.InternalComms

# Now try to find logs across 5 terminals...
```

### With Aspire (One Command):
```bash
dotnet run --project src/AgentHub.AppHost
# ✅ All services start automatically
# ✅ Dashboard opens with all logs
# ✅ Distributed tracing enabled
# ✅ Service Bus provisioned
# ✅ Health checks automatic
```

## Observability Features

### Distributed Tracing

Aspire automatically instruments traces showing:

```
Customer Inquiry Event Published
  ↓
Customer Experience Agent Receives
  ↓ (Analyzes with LLM)
  ↓
Publishes: customer.inquiry.analyzed
  ↓
Operations Agent Receives
  ↓ (Checks inventory, LLM decides)
  ↓
Publishes: operations.inventory.checked
  ↓
Financial Agent Receives
  ↓ (Authorizes payment, LLM assesses risk)
  ↓
Publishes: financial.authorization.completed
  ↓
All Agents + Internal Comms Observes
```

**In the Aspire Dashboard**, you'll see this entire flow as a single distributed trace with:
- Timing information
- LLM function calls
- External system calls (Order, Inventory, Payment, Teams)
- Event publishing/receiving

### Structured Logging

All agent logs are centralized in the Aspire Dashboard with:
- Filtering by agent
- Filtering by log level
- Full-text search
- Timestamp ordering

### Metrics

Real-time metrics:
- Event processing rate
- LLM call latency
- External system response times
- Memory/CPU usage per agent

## Azure Deployment

When ready for Azure, Aspire can generate infrastructure:

```bash
# Generate Azure resource definitions
azd init

# Provision and deploy to Azure
azd up

# This creates:
# - Azure Container Apps for each service
# - Azure Service Bus (real, not emulator)
# - Azure Application Insights
# - Azure Container Registry
# - Managed identities
```

## Configuration

### Environment Variables

Aspire automatically injects:
- `AGENTBUS_URL` - Broker endpoint (from service discovery)
- `ConnectionStrings__servicebus` - Service Bus connection string
- `APPLICATIONINSIGHTS_CONNECTION_STRING` - App Insights key

### Service Dependencies

In [AppHost.cs](src/AgentHub.AppHost/AppHost.cs):

```csharp
// AgentBus Broker
var broker = builder.AddProject<Projects.AgentBus_Broker>("agentbus-broker")
    .WithReference(serviceBus)
    .WithHttpEndpoint(port: 5000);

// Agents automatically get broker URL via service discovery
var customerAgent = builder.AddProject<Projects.AgentBus_Examples_CustomerExperience>("customer...")
    .WithReference(broker);  // 👈 Service discovery magic!
```

## Troubleshooting

### Port Conflicts

If port 5000 is in use:

```csharp
// In AppHost.cs, change:
.WithHttpEndpoint(port: 5000, name: "http")
// To:
.WithHttpEndpoint(port: 5001, name: "http")
```

### Service Bus Emulator Not Starting

Ensure Docker Desktop is running:

```bash
docker ps
# Should show Service Bus emulator container
```

### Dashboard Not Opening

Check the console output for the dashboard URL. It may be different:

```
Now listening on: https://localhost:17238
```

## Comparison: Regular vs Aspire

| Feature | Manual Execution | .NET Aspire |
|---------|-----------------|-------------|
| **Start Services** | 5 terminals | 1 command |
| **View Logs** | Switch between terminals | Unified dashboard |
| **Distributed Tracing** | Manual correlation | Automatic |
| **Service Discovery** | Hardcoded URLs | Automatic |
| **Health Checks** | Manual checks | Built-in |
| **Azure Deployment** | Manual ARM/Bicep | `azd up` |
| **Local Service Bus** | Need real Azure | Docker emulator |
| **Developer Experience** | 😓 Manual | 🚀 Automated |

## What's Orchestrated

1. **AgentBus.Broker** (net10.0)
   - Core message broker
   - HTTP API on port 5000
   - Connects to Service Bus

2. **Customer Experience Agent** (net9.0)
   - HTTP transport
   - Autonomous LLM decision-making
   - References Order System

3. **Operations & Inventory Agent** (net9.0)
   - Service Bus transport
   - Supply chain optimization
   - References Inventory DB

4. **Financial Authorization Agent** (net9.0)
   - HTTP transport
   - Risk assessment
   - References Payment Gateway

5. **Internal Communications Agent** (net9.0)
   - Service Bus transport
   - Global event observer
   - References Teams API

6. **Azure Service Bus Emulator**
   - Runs in Docker
   - Fully compatible with real Azure Service Bus
   - No cloud costs for local dev

## Next Steps

1. **Set LLM API Key** for real autonomous behavior:
   ```bash
   dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project src/AgentHub.AppHost
   ```

2. **Explore the Dashboard**: Run the AppHost and explore all the observability features

3. **Deploy to Azure**: Run `azd init` and `azd up` to deploy to Azure Container Apps

---

**This is modern .NET development - orchestration, observability, and cloud-native deployment built-in!**
