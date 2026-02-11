# AgentBus — Agent-to-Agent Communication Broker

## Architecture Design Document

**Version:** 0.1 (MVP/PoC)
**Date:** 2026-02-11
**Status:** Draft

---

## 1. Executive Summary

AgentBus is an agent-to-agent (A2A) communication broker that enables autonomous agents to discover each other, exchange direct messages, and broadcast/subscribe to events. The system uses a **service discovery pattern** with a centralized agent registry, backed by messaging infrastructure for reliable, decoupled communication.

**USP:** The MVP is designed with clear seams so every component can be swapped for its enterprise-grade equivalent without re-architecting.

---

## 2. Key Requirements

| Category | MVP | Enterprise Upgrade Path |
|---|---|---|
| **Direct Messaging** | Point-to-point via Service Bus queues | Partitioned queues, sessions, priority |
| **Event Broadcasting** | Pub/sub via Service Bus topics | Event Grid + Service Bus hybrid |
| **Service Discovery** | Cosmos DB serverless registry | Cosmos DB provisioned + geo-replication |
| **Agent Identity** | Managed Identity (user-assigned) | Managed Identity + Entra ID app roles + custom claims |
| **Broker API** | Azure Container Apps (single region) | Multi-region Container Apps + APIM front door |
| **Observability** | Application Insights | Azure Monitor full stack + distributed tracing |

---

## 3. High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        AgentBus Platform                            │
│                                                                     │
│  ┌──────────┐    ┌──────────────────┐    ┌───────────────────────┐  │
│  │  Agent A  │───▶│   Broker API     │◀───│  Agent B              │  │
│  │  (MI)     │    │  (Container Apps)│    │  (MI)                 │  │
│  └──────────┘    └────────┬─────────┘    └───────────────────────┘  │
│                           │                                         │
│              ┌────────────┼────────────────┐                        │
│              │            │                │                        │
│              ▼            ▼                ▼                        │
│  ┌───────────────┐ ┌──────────┐ ┌─────────────────┐               │
│  │ Agent Registry │ │ Service  │ │  Event Topics   │               │
│  │ (Cosmos DB     │ │ Bus      │ │  (Service Bus   │               │
│  │  Serverless)   │ │ Queues   │ │   Topics)       │               │
│  └───────────────┘ └──────────┘ └─────────────────┘               │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                  Observability (App Insights)                │   │
│  └─────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 4. Component Design

### 4.1 Agent Registry (Service Discovery)

**Service:** Azure Cosmos DB (NoSQL API, Serverless)

The Agent Registry is the backbone of the discovery pattern. Each agent registers itself, declaring its capabilities, supported message types, and endpoint metadata.

**Data Model:**

```json
{
  "id": "agent-drone-scheduler-01",
  "partitionKey": "agents",
  "name": "DroneScheduler",
  "version": "1.0.0",
  "status": "active",
  "capabilities": [
    {
      "name": "schedule-drone",
      "description": "Schedule a drone for package delivery",
      "inputSchema": { "$ref": "#/schemas/ScheduleDroneRequest" },
      "outputSchema": { "$ref": "#/schemas/ScheduleDroneResponse" }
    }
  ],
  "messageTypes": {
    "accepts": ["schedule-drone-request", "cancel-drone-request"],
    "emits": ["drone-scheduled", "drone-cancelled", "drone-in-transit"]
  },
  "identity": {
    "managedIdentityClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "principalId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  },
  "endpoints": {
    "directQueue": "agent-drone-scheduler-01-inbox",
    "healthCheck": "https://drone-scheduler.internal/health"
  },
  "metadata": {
    "owner": "logistics-team",
    "environment": "production",
    "tags": ["logistics", "drone", "delivery"]
  },
  "registeredAt": "2026-02-11T10:00:00Z",
  "lastHeartbeat": "2026-02-11T10:05:00Z",
  "ttl": -1
}
```

**Registry Operations:**

| Operation | Description | API |
|---|---|---|
| **Register** | Agent registers itself with capabilities | `POST /agents` |
| **Deregister** | Agent removes itself | `DELETE /agents/{id}` |
| **Heartbeat** | Periodic liveness signal | `PATCH /agents/{id}/heartbeat` |
| **Discover** | Find agents by capability | `GET /agents?capability={name}` |
| **Get** | Retrieve agent details | `GET /agents/{id}` |
| **List** | List all registered agents | `GET /agents` |

**Enterprise Upgrade Path:**
- Switch Cosmos DB to provisioned throughput with geo-replication
- Add change feed processing for real-time registry event streaming
- Implement agent versioning with side-by-side capability registration

---

### 4.2 Messaging — Direct Messages

**Service:** Azure Service Bus (Standard tier)

For point-to-point agent communication. Each registered agent gets a dedicated inbox queue, auto-provisioned at registration time.

**Queue Naming Convention:** `agent-{agentId}-inbox`

**Message Envelope:**

```json
{
  "messageId": "msg-xxxxxxxx",
  "correlationId": "corr-xxxxxxxx",
  "conversationId": "conv-xxxxxxxx",
  "from": "agent-package-service-01",
  "to": "agent-drone-scheduler-01",
  "messageType": "schedule-drone-request",
  "timestamp": "2026-02-11T10:05:00Z",
  "ttl": "PT1H",
  "payload": {
    "packageId": "pkg-123",
    "destination": { "lat": 47.6062, "lng": -122.3321 },
    "priority": "standard"
  },
  "replyTo": "agent-package-service-01-inbox",
  "headers": {
    "x-trace-id": "trace-xxxxxxxx",
    "x-agent-version": "1.0.0"
  }
}
```

**Direct Message Flow:**
```
Agent A                    Broker API                Service Bus           Agent B
  │                           │                         │                    │
  │  POST /messages/send      │                         │                    │
  │  {to: "agent-b", ...}     │                         │                    │
  │──────────────────────────▶│                         │                    │
  │                           │  Validate sender MI     │                    │
  │                           │  Lookup target queue    │                    │
  │                           │  from Registry          │                    │
  │                           │────────────────────────▶│                    │
  │                           │  Enqueue to             │                    │
  │                           │  agent-b-inbox          │                    │
  │  202 Accepted             │                         │                    │
  │◀──────────────────────────│                         │                    │
  │                           │                         │  Receive message   │
  │                           │                         │◀───────────────────│
  │                           │                         │  (pull / long-poll)│
```

**Enterprise Upgrade Path:**
- Service Bus Premium tier for message sessions and VNET integration
- Partitioned queues for high-throughput agents
- Auto-forwarding for message routing chains
- Dead-letter queue monitoring with automated remediation

---

### 4.3 Messaging — Events (Pub/Sub)

**Service:** Azure Service Bus Topics + Subscriptions

Agents publish domain events to well-known topics. Other agents subscribe to events they're interested in, optionally filtered by rules.

**Topic Naming Convention:** `events.{domain}.{event-type}`
Examples:
- `events.logistics.drone-scheduled`
- `events.logistics.package-shipped`
- `events.system.agent-registered`

**Subscription Model:**
When an agent registers interest in an event type, the Broker API creates a subscription on the appropriate topic with optional SQL filter rules.

**Event Envelope:**

```json
{
  "eventId": "evt-xxxxxxxx",
  "eventType": "drone-scheduled",
  "source": "agent-drone-scheduler-01",
  "timestamp": "2026-02-11T10:06:00Z",
  "dataVersion": "1.0",
  "data": {
    "droneId": "drone-42",
    "packageId": "pkg-123",
    "estimatedArrival": "2026-02-11T11:30:00Z"
  },
  "headers": {
    "x-trace-id": "trace-xxxxxxxx",
    "x-correlation-id": "corr-xxxxxxxx"
  }
}
```

**Event Flow:**
```
Agent A                 Broker API              Service Bus Topic       Agent B, C
  │                        │                         │                    │
  │ POST /events/publish   │                         │                    │
  │ {type: "drone-sched"}  │                         │                    │
  │───────────────────────▶│                         │                    │
  │                        │  Publish to topic       │                    │
  │                        │  events.logistics.      │                    │
  │                        │  drone-scheduled        │                    │
  │                        │────────────────────────▶│                    │
  │  202 Accepted          │                         │                    │
  │◀───────────────────────│                         │  Fan-out to subs   │
  │                        │                         │───────────────────▶│
  │                        │                         │  Agent B sub       │
  │                        │                         │  Agent C sub       │
```

**Enterprise Upgrade Path:**
- Hybrid with Azure Event Grid for system-level events and webhooks
- Topic auto-forwarding for event chaining
- Content-based routing rules per subscription
- Event Hubs integration for high-volume event streaming and analytics

---

### 4.4 Broker API

**Service:** Azure Container Apps (Consumption plan)

The Broker API is the central control plane that agents interact with. It handles registration, discovery, message routing, and event management.

**API Surface:**

```
# Agent Registry
POST   /api/v1/agents                        # Register agent
DELETE /api/v1/agents/{agentId}              # Deregister
PATCH  /api/v1/agents/{agentId}/heartbeat    # Heartbeat
GET    /api/v1/agents                        # List / search agents
GET    /api/v1/agents/{agentId}              # Get agent details
GET    /api/v1/agents/discover?capability=X  # Discover by capability

# Direct Messaging
POST   /api/v1/messages/send                 # Send direct message
GET    /api/v1/messages/receive              # Receive from agent inbox (long-poll)
POST   /api/v1/messages/{messageId}/ack      # Acknowledge processed

# Events
POST   /api/v1/events/publish                # Publish event
POST   /api/v1/events/subscribe              # Subscribe to event type
DELETE /api/v1/events/subscriptions/{subId}  # Unsubscribe
GET    /api/v1/events/receive/{subId}        # Receive events (long-poll)

# System
GET    /api/v1/health                        # Health check
GET    /api/v1/health/ready                  # Readiness probe
```

**Technology Stack (MVP):**
- Runtime: .NET +10 / C# +14 / ASP.NET Core Minimal APIs
- Architecture: Modular Monolith with Vertical Slice Architecture (see Section 4.7)
- Serialization: System.Text.Json (source-generated)
- Service Bus SDK: Azure.Messaging.ServiceBus
- Cosmos DB SDK: Microsoft.Azure.Cosmos
- Auth: Microsoft.Identity.Web (Managed Identity validation)
- Testing: xUnit + Shouldly + Reqnroll (Gherkin BDD)
- Frontend (optional): React 19 + TypeScript + Vite (admin dashboard)

**Enterprise Upgrade Path:**
- Azure API Management (APIM) front door for rate limiting, API versioning, developer portal
- Multi-region deployment with Azure Front Door
- gRPC support for high-performance inter-agent communication
- WebSocket support for real-time event streaming
- Extract modules into independent deployable services when scale demands

---

### 4.5 Agent Identity & Security

**Service:** Microsoft Entra ID + User-Assigned Managed Identities

Each agent is an Azure compute resource (Container App, Function, VM) with a **user-assigned managed identity**. This identity is the agent's credential — no secrets, keys, or certificates to manage.

**Identity Flow:**

```
Agent (Container App)         Entra ID              Broker API
  │                              │                      │
  │  Request token for           │                      │
  │  AgentBus audience           │                      │
  │  (via Azure.Identity SDK)    │                      │
  │─────────────────────────────▶│                      │
  │                              │                      │
  │  JWT token (MI cred)         │                      │
  │◀─────────────────────────────│                      │
  │                              │                      │
  │  API call + Bearer token     │                      │
  │─────────────────────────────────────────────────────▶│
  │                              │                      │
  │                              │  Validate JWT        │
  │                              │◀─────────────────────│
  │                              │  Check role/scope    │
  │                              │─────────────────────▶│
  │                              │                      │
  │  200 OK / 403 Forbidden      │                      │
  │◀─────────────────────────────────────────────────────│
```

**Authorization Model (MVP):**

| Role | Permissions |
|---|---|
| `AgentBus.Agent` | Register self, send/receive messages, publish/subscribe events |
| `AgentBus.Admin` | Manage all agents, view all messages, system configuration |

**Implementation:**
1. Create an **Entra ID App Registration** for the Broker API (defines the resource/audience)
2. Define **App Roles** (`AgentBus.Agent`, `AgentBus.Admin`) in the app manifest
3. Assign app roles to each agent's managed identity service principal
4. Broker API validates the `roles` claim in the JWT on each request

**Enterprise Upgrade Path:**
- Fine-grained RBAC: per-topic publish/subscribe permissions
- Entra ID custom security attributes for agent metadata
- Conditional Access policies (e.g., only from specific VNETs)
- Private endpoints for all data-plane services (Service Bus, Cosmos DB)
- mTLS between agents for defense-in-depth

---

### 4.6 Observability

**Service:** Azure Application Insights + Azure Monitor

| Signal | MVP Implementation |
|---|---|
| **Distributed Tracing** | OpenTelemetry SDK → App Insights, correlation via `x-trace-id` header |
| **Metrics** | Custom metrics: messages/sec, agents registered, event fan-out count |
| **Logging** | Structured logging (Serilog → App Insights) |
| **Health** | `/health` and `/health/ready` endpoints |
| **Alerting** | Basic alerts: DLQ depth > 0, agent heartbeat missed |

**Enterprise Upgrade Path:**
- Azure Monitor Workbooks for operational dashboards
- Azure Log Analytics for cross-service query/correlation
- Grafana integration for custom dashboards
- SLA/SLO monitoring with burn-rate alerts

---

### 4.7 Software Architecture — Modular Monolith with Vertical Slices

The Broker API follows a **Modular Monolith** pattern internally, with each domain area implemented as an independent module. Within each module, features are organized as **Vertical Slices** — each slice owns its request, handler, validation, persistence, and response end-to-end.

This gives us the development simplicity of a monolith with the architectural boundaries of microservices — and the option to extract any module into its own service later.

#### 4.7.1 Bounded Contexts / Modules

```
┌─────────────────────────────────────────────────────────────────┐
│                    AgentBus Broker API                           │
│                   (Single Deployable Unit)                       │
│                                                                 │
│  ┌─────────────────┐  ┌─────────────────┐  ┌────────────────┐  │
│  │   Registration   │  │    Messaging     │  │    Eventing    │  │
│  │     Module       │  │     Module       │  │     Module     │  │
│  │                 │  │                 │  │                │  │
│  │ • Register      │  │ • SendMessage   │  │ • Publish      │  │
│  │ • Deregister    │  │ • ReceiveMsg    │  │ • Subscribe    │  │
│  │ • Heartbeat     │  │ • AcknowledgeMsg│  │ • Unsubscribe  │  │
│  │ • Discover      │  │                 │  │ • ReceiveEvent │  │
│  │ • GetAgent      │  │                 │  │                │  │
│  └────────┬────────┘  └────────┬────────┘  └───────┬────────┘  │
│           │                    │                    │            │
│  ┌────────┴────────────────────┴────────────────────┴────────┐  │
│  │                    Shared Kernel                           │  │
│  │  • Domain primitives (AgentId, MessageEnvelope, etc.)     │  │
│  │  • Cross-cutting: Auth, Logging, Error handling           │  │
│  │  • Service Bus / Cosmos DB abstractions                   │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

| Module | Bounded Context | Owns | External Dependency |
|---|---|---|---|
| **Registration** | Agent identity & capabilities | `agents` Cosmos container, inbox queue lifecycle | Cosmos DB, Service Bus (admin) |
| **Messaging** | Point-to-point communication | Message routing & inbox queues | Service Bus (data plane) |
| **Eventing** | Pub/sub event distribution | Topics, subscriptions, fan-out | Service Bus (data plane) |
| **Shared Kernel** | Cross-cutting concerns | Domain primitives, abstractions, middleware | — |

**Module Interaction Rules:**
- Modules communicate through **well-defined internal contracts** (interfaces in Shared Kernel), never by reaching into each other's internals
- Each module has its own directory, its own DI registration, and its own feature slices
- No circular dependencies — dependency flows inward toward Shared Kernel

#### 4.7.2 Vertical Slice Architecture

Each feature is a self-contained slice: request → validation → handler → persistence → response. No layered abstractions (no "repository layer", no "service layer" spanning features).

**Slice anatomy:**

```
Feature: RegisterAgent
├── RegisterAgentEndpoint.cs      // Minimal API endpoint mapping
├── RegisterAgentRequest.cs       // Request DTO (record)
├── RegisterAgentResponse.cs      // Response DTO (record)
├── RegisterAgentValidator.cs     // FluentValidation rules
├── RegisterAgentHandler.cs       // Business logic + persistence
└── RegisterAgentTests/
    ├── RegisterAgent.feature      // Gherkin BDD scenario
    ├── RegisterAgentSteps.cs      // Step definitions
    └── RegisterAgentHandlerTests.cs // Unit tests (xUnit + Shouldly)
```

**Example — RegisterAgent slice:**

```csharp
// RegisterAgentRequest.cs
namespace AgentBus.Modules.Registration.Features.RegisterAgent;

public sealed record RegisterAgentRequest(
    string Name,
    string Version,
    List<AgentCapability> Capabilities,
    List<string> Accepts,
    List<string> Emits
);

public sealed record AgentCapability(string Name, string Description);
```

```csharp
// RegisterAgentHandler.cs
namespace AgentBus.Modules.Registration.Features.RegisterAgent;

public sealed class RegisterAgentHandler(
    IAgentRepository agentRepository,
    IInboxProvisioner inboxProvisioner,
    IEventPublisher eventPublisher,
    TimeProvider timeProvider)
{
    public async Task<Result<RegisterAgentResponse>> HandleAsync(
        RegisterAgentRequest request,
        ClaimsPrincipal caller,
        CancellationToken ct = default)
    {
        var agentId = AgentId.From(caller);

        var agent = new AgentDocument
        {
            Id = agentId.Value,
            Name = request.Name,
            Version = request.Version,
            Status = AgentStatus.Active,
            Capabilities = request.Capabilities,
            MessageTypes = new(request.Accepts, request.Emits),
            RegisteredAt = timeProvider.GetUtcNow(),
            LastHeartbeat = timeProvider.GetUtcNow()
        };

        await agentRepository.UpsertAsync(agent, ct);
        await inboxProvisioner.EnsureQueueAsync(agentId, ct);
        await eventPublisher.PublishAsync(
            new AgentRegisteredEvent(agentId, request.Name), ct);

        return new RegisterAgentResponse(agentId.Value, agent.Endpoints.DirectQueue);
    }
}
```

```csharp
// RegisterAgentEndpoint.cs
namespace AgentBus.Modules.Registration.Features.RegisterAgent;

public static class RegisterAgentEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/agents", async (
            RegisterAgentRequest request,
            RegisterAgentHandler handler,
            RegisterAgentValidator validator,
            ClaimsPrincipal caller,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var result = await handler.HandleAsync(request, caller, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/agents/{result.Value.AgentId}", result.Value)
                : Results.Problem(result.Error);
        })
        .RequireAuthorization("AgentBus.Agent")
        .WithTags("Registration")
        .WithName("RegisterAgent")
        .Produces<RegisterAgentResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem();
}
```

#### 4.7.3 Solution & Project Structure

```
AgentBus/
├── src/
│   ├── AgentBus.Api/                          # Host / entry point
│   │   ├── Program.cs                         # Minimal API host, module registration
│   │   ├── appsettings.json
│   │   └── AgentBus.Api.csproj
│   │
│   ├── AgentBus.Modules.Registration/         # Registration bounded context
│   │   ├── Features/
│   │   │   ├── RegisterAgent/
│   │   │   │   ├── RegisterAgentEndpoint.cs
│   │   │   │   ├── RegisterAgentRequest.cs
│   │   │   │   ├── RegisterAgentResponse.cs
│   │   │   │   ├── RegisterAgentValidator.cs
│   │   │   │   └── RegisterAgentHandler.cs
│   │   │   ├── DeregisterAgent/
│   │   │   ├── Heartbeat/
│   │   │   ├── DiscoverAgents/
│   │   │   └── GetAgent/
│   │   ├── Domain/
│   │   │   └── AgentDocument.cs               # Persistence model
│   │   ├── Infrastructure/
│   │   │   ├── AgentRepository.cs             # Cosmos DB access
│   │   │   └── InboxProvisioner.cs            # Service Bus queue mgmt
│   │   ├── RegistrationModule.cs              # DI + endpoint registration
│   │   └── AgentBus.Modules.Registration.csproj
│   │
│   ├── AgentBus.Modules.Messaging/            # Messaging bounded context
│   │   ├── Features/
│   │   │   ├── SendMessage/
│   │   │   ├── ReceiveMessage/
│   │   │   └── AcknowledgeMessage/
│   │   ├── Domain/
│   │   ├── Infrastructure/
│   │   ├── MessagingModule.cs
│   │   └── AgentBus.Modules.Messaging.csproj
│   │
│   ├── AgentBus.Modules.Eventing/             # Eventing bounded context
│   │   ├── Features/
│   │   │   ├── PublishEvent/
│   │   │   ├── Subscribe/
│   │   │   ├── Unsubscribe/
│   │   │   └── ReceiveEvents/
│   │   ├── Domain/
│   │   ├── Infrastructure/
│   │   ├── EventingModule.cs
│   │   └── AgentBus.Modules.Eventing.csproj
│   │
│   ├── AgentBus.SharedKernel/                 # Cross-cutting shared code
│   │   ├── Domain/
│   │   │   ├── AgentId.cs                     # Strongly-typed ID
│   │   │   ├── MessageEnvelope.cs
│   │   │   ├── EventEnvelope.cs
│   │   │   └── Result.cs                      # Result<T> discriminated union
│   │   ├── Auth/
│   │   │   └── ClaimsPrincipalExtensions.cs
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   └── CorrelationIdMiddleware.cs
│   │   ├── Abstractions/
│   │   │   ├── IEventPublisher.cs
│   │   │   ├── IAgentRepository.cs
│   │   │   └── IInboxProvisioner.cs
│   │   └── AgentBus.SharedKernel.csproj
│   │
│   └── AgentBus.Sdk/                          # Agent client SDK (NuGet)
│       ├── AgentBusClient.cs
│       ├── AgentBusOptions.cs
│       └── AgentBus.Sdk.csproj
│
├── tests/
│   ├── AgentBus.Tests.Unit/                   # Unit tests (xUnit + Shouldly)
│   │   ├── Modules/
│   │   │   ├── Registration/
│   │   │   │   ├── RegisterAgentHandlerTests.cs
│   │   │   │   ├── DeregisterAgentHandlerTests.cs
│   │   │   │   └── DiscoverAgentsHandlerTests.cs
│   │   │   ├── Messaging/
│   │   │   │   └── SendMessageHandlerTests.cs
│   │   │   └── Eventing/
│   │   │       └── PublishEventHandlerTests.cs
│   │   └── AgentBus.Tests.Unit.csproj
│   │
│   ├── AgentBus.Tests.BDD/                    # BDD specs (Reqnroll + Gherkin)
│   │   ├── Features/
│   │   │   ├── Registration/
│   │   │   │   ├── RegisterAgent.feature
│   │   │   │   └── DiscoverAgents.feature
│   │   │   ├── Messaging/
│   │   │   │   └── DirectMessaging.feature
│   │   │   └── Eventing/
│   │   │       └── EventPubSub.feature
│   │   ├── StepDefinitions/
│   │   │   ├── RegistrationSteps.cs
│   │   │   ├── MessagingSteps.cs
│   │   │   └── EventingSteps.cs
│   │   ├── Hooks/
│   │   │   └── TestHostHook.cs                # WebApplicationFactory setup
│   │   └── AgentBus.Tests.BDD.csproj
│   │
│   └── AgentBus.Tests.Integration/            # Integration tests
│       ├── BrokerApiFixture.cs                # Shared test fixture
│       ├── Registration/
│       ├── Messaging/
│       ├── Eventing/
│       └── AgentBus.Tests.Integration.csproj
│
├── dashboard/                                 # Optional admin UI
│   ├── src/
│   │   ├── App.tsx
│   │   ├── components/
│   │   │   ├── AgentList.tsx
│   │   │   ├── MessageMonitor.tsx
│   │   │   └── EventStream.tsx
│   │   ├── hooks/
│   │   ├── api/
│   │   └── types/
│   ├── package.json
│   ├── tsconfig.json
│   └── vite.config.ts
│
├── infra/                                     # Bicep IaC
│   ├── main.bicep
│   ├── modules/
│   └── parameters/
│
├── AgentBus.sln
├── Directory.Build.props                      # Shared build properties
├── Directory.Packages.props                   # Central package management
├── .editorconfig                              # C# coding standards
└── global.json                                # Pin .NET 9 SDK
```

#### 4.7.4 Module Registration Pattern

Each module exposes a single extension method to register its services and endpoints:

```csharp
// RegistrationModule.cs
namespace AgentBus.Modules.Registration;

public static class RegistrationModule
{
    public static IServiceCollection AddRegistrationModule(this IServiceCollection services)
    {
        services.AddScoped<RegisterAgentHandler>();
        services.AddScoped<RegisterAgentValidator>();
        services.AddScoped<IAgentRepository, CosmosAgentRepository>();
        services.AddScoped<IInboxProvisioner, ServiceBusInboxProvisioner>();
        return services;
    }

    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        RegisterAgentEndpoint.Map(app);
        DeregisterAgentEndpoint.Map(app);
        HeartbeatEndpoint.Map(app);
        DiscoverAgentsEndpoint.Map(app);
        GetAgentEndpoint.Map(app);
        return app;
    }
}
```

```csharp
// Program.cs — Host composition root
var builder = WebApplication.CreateBuilder(args);

// Module registration
builder.Services
    .AddSharedKernel(builder.Configuration)
    .AddRegistrationModule()
    .AddMessagingModule()
    .AddEventingModule();

var app = builder.Build();

// Module endpoint mapping
app.MapRegistrationEndpoints();
app.MapMessagingEndpoints();
app.MapEventingEndpoints();

app.Run();
```

#### 4.7.5 C# Standards & Conventions

| Standard | Enforcement |
|---|---|
| **Target Framework** | .NET 9 (LTS), C# 13 |
| **Nullable reference types** | Enabled globally (`<Nullable>enable</Nullable>`) |
| **Implicit usings** | Enabled |
| **File-scoped namespaces** | Required |
| **Primary constructors** | Preferred for DI injection |
| **Records** | For all DTOs, requests, responses, events, value objects |
| **`required` modifier** | For mandatory properties on mutable types |
| **Collection expressions** | Use `[item1, item2]` syntax |
| **Pattern matching** | Preferred over `if`/`switch` chains |
| **Raw string literals** | For inline JSON, SQL, templates |
| **`sealed`** | All classes sealed by default unless designed for inheritance |
| **`TimeProvider`** | Injected (never `DateTime.UtcNow` directly) — testable |
| **`CancellationToken`** | Required on all async methods |
| **Central Package Management** | `Directory.Packages.props` — single source of truth for NuGet versions |
| **EditorConfig** | `.editorconfig` enforcing formatting, naming, style rules |
| **Analyzers** | `Microsoft.CodeAnalysis.NetAnalyzers` + `StyleCop.Analyzers` at Warning level |

**`Directory.Build.props` (shared):**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>13</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

#### 4.7.6 Testing Strategy — BDD + TDD

Testing follows an **outside-in** approach: start with a Gherkin BDD scenario describing the desired behavior, then drive the implementation through unit-level TDD cycles.

```
BDD Scenario (Gherkin)          →  Defines WHAT the system should do
  └── Integration test            →  Verifies the full slice end-to-end
       └── TDD Unit tests         →  Drives HOW the handler is built
            └── Implementation    →  Production code
```

**Testing Stack:**

| Layer | Tool | Purpose |
|---|---|---|
| **BDD Specs** | Reqnroll 2.x + Gherkin | Living documentation, stakeholder-readable acceptance criteria |
| **Unit Tests** | xUnit 2.x + Shouldly | Fast, isolated handler/validation/domain logic tests |
| **Integration Tests** | xUnit + `WebApplicationFactory` | Full HTTP pipeline tests with real DI container |
| **Mocking** | NSubstitute | Lightweight interface mocking for unit tests |
| **Test Data** | Bogus | Realistic fake data generation |

**BDD Gherkin Example — Agent Registration:**

```gherkin
# Features/Registration/RegisterAgent.feature

Feature: Agent Registration
  As an autonomous agent
  I want to register myself with the AgentBus broker
  So that other agents can discover my capabilities

  Background:
    Given the AgentBus broker is running
    And I am authenticated with a valid managed identity

  Scenario: Successfully register a new agent
    Given I have not previously registered
    When I register with the following details:
      | Name            | Version | Capability      | Description                        |
      | DroneScheduler  | 1.0.0   | schedule-drone  | Schedule a drone for delivery      |
    Then I should receive a 201 Created response
    And the response should contain my agent ID
    And my inbox queue should be provisioned
    And an "agent-registered" system event should be published

  Scenario: Re-register an existing agent updates capabilities
    Given I am already registered as "DroneScheduler" version "1.0.0"
    When I register with version "1.1.0" and additional capability "cancel-drone"
    Then I should receive a 201 Created response
    And my agent record should show version "1.1.0"
    And my capabilities should include "schedule-drone" and "cancel-drone"

  Scenario: Registration is rejected without authentication
    Given I am not authenticated
    When I attempt to register
    Then I should receive a 401 Unauthorized response
```

**BDD Gherkin Example — Service Discovery:**

```gherkin
# Features/Registration/DiscoverAgents.feature

Feature: Agent Discovery
  As an agent looking for collaborators
  I want to discover agents by their capabilities
  So that I can send them targeted messages

  Scenario: Discover agents by capability
    Given the following agents are registered:
      | Name            | Capability       |
      | DroneScheduler  | schedule-drone   |
      | PackageService  | track-package    |
      | DroneFleet      | schedule-drone   |
    When I search for agents with capability "schedule-drone"
    Then I should find 2 agents
    And the results should include "DroneScheduler" and "DroneFleet"

  Scenario: No agents match the requested capability
    Given no agents with capability "time-travel" are registered
    When I search for agents with capability "time-travel"
    Then I should find 0 agents
    And the response should be an empty list
```

**BDD Gherkin Example — Direct Messaging:**

```gherkin
# Features/Messaging/DirectMessaging.feature

Feature: Direct Agent-to-Agent Messaging
  As an agent
  I want to send messages directly to another agent
  So that I can request actions or exchange information

  Scenario: Send a message to a registered agent
    Given agent "PackageService" is registered
    And agent "DroneScheduler" is registered
    When "PackageService" sends a "schedule-drone-request" message to "DroneScheduler"
    Then the broker should return 202 Accepted
    And the message should be enqueued in "DroneScheduler"'s inbox

  Scenario: Sending a message to an unregistered agent fails
    Given agent "PackageService" is registered
    And agent "GhostAgent" is not registered
    When "PackageService" sends a message to "GhostAgent"
    Then the broker should return 404 Not Found
```

**BDD Gherkin Example — Event Pub/Sub:**

```gherkin
# Features/Eventing/EventPubSub.feature

Feature: Event Publishing and Subscription
  As an agent
  I want to publish events and subscribe to event types
  So that I can participate in event-driven workflows

  Scenario: Publish an event to subscribed agents
    Given agent "DroneScheduler" is registered
    And agent "DeliveryTracker" is subscribed to "drone-scheduled" events
    When "DroneScheduler" publishes a "drone-scheduled" event
    Then the broker should return 202 Accepted
    And "DeliveryTracker" should receive the event

  Scenario: Agents only receive events they subscribed to
    Given agent "DeliveryTracker" is subscribed to "drone-scheduled" events
    And agent "DeliveryTracker" is not subscribed to "package-shipped" events
    When a "package-shipped" event is published
    Then "DeliveryTracker" should not receive the event
```

**Step Definition Example:**

```csharp
// StepDefinitions/RegistrationSteps.cs
namespace AgentBus.Tests.BDD.StepDefinitions;

[Binding]
public sealed class RegistrationSteps(
    BrokerApiFixture fixture,
    ScenarioContext context)
{
    [Given(@"I have not previously registered")]
    public void GivenIHaveNotPreviouslyRegistered()
    {
        // No-op — clean state per scenario
    }

    [When(@"I register with the following details:")]
    public async Task WhenIRegisterWithTheFollowingDetails(Table table)
    {
        var row = table.Rows[0];
        var request = new RegisterAgentRequest(
            Name: row["Name"],
            Version: row["Version"],
            Capabilities: [new(row["Capability"], row["Description"])],
            Accepts: [$"{row["Capability"]}-request"],
            Emits: [row["Capability"]]
        );

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/agents", request);
        context["response"] = response;
    }

    [Then(@"I should receive a (\d+) .+ response")]
    public void ThenIShouldReceiveResponse(int statusCode)
    {
        var response = context.Get<HttpResponseMessage>("response");
        ((int)response.StatusCode).ShouldBe(statusCode);
    }

    [Then(@"the response should contain my agent ID")]
    public async Task ThenTheResponseShouldContainMyAgentId()
    {
        var response = context.Get<HttpResponseMessage>("response");
        var body = await response.Content.ReadFromJsonAsync<RegisterAgentResponse>();
        body.ShouldNotBeNull();
        body.AgentId.ShouldNotBeNullOrWhiteSpace();
    }
}
```

**Unit Test Example (xUnit + Shouldly):**

```csharp
// Tests/Unit/Modules/Registration/RegisterAgentHandlerTests.cs
namespace AgentBus.Tests.Unit.Modules.Registration;

public sealed class RegisterAgentHandlerTests
{
    private readonly IAgentRepository _agentRepo = Substitute.For<IAgentRepository>();
    private readonly IInboxProvisioner _inboxProvisioner = Substitute.For<IInboxProvisioner>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly RegisterAgentHandler _sut;

    public RegisterAgentHandlerTests()
    {
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 2, 11, 10, 0, 0, TimeSpan.Zero));
        _sut = new(_agentRepo, _inboxProvisioner, _eventPublisher, _timeProvider);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_CreatesAgentAndProvisionInbox()
    {
        // Arrange
        var request = new RegisterAgentRequest(
            Name: "DroneScheduler",
            Version: "1.0.0",
            Capabilities: [new("schedule-drone", "Schedule a drone")],
            Accepts: ["schedule-drone-request"],
            Emits: ["drone-scheduled"]
        );
        var caller = FakeClaims.AgentPrincipal("agent-drone-01");

        // Act
        var result = await _sut.HandleAsync(request, caller, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AgentId.ShouldBe("agent-drone-01");

        await _agentRepo.Received(1).UpsertAsync(
            Arg.Is<AgentDocument>(a => a.Name == "DroneScheduler"),
            Arg.Any<CancellationToken>());

        await _inboxProvisioner.Received(1).EnsureQueueAsync(
            Arg.Is<AgentId>(id => id.Value == "agent-drone-01"),
            Arg.Any<CancellationToken>());

        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<AgentRegisteredEvent>(e => e.AgentId.Value == "agent-drone-01"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_SetsCorrectTimestamps()
    {
        // Arrange
        var request = new RegisterAgentRequest(
            "TestAgent", "1.0.0", [], [], []);
        var caller = FakeClaims.AgentPrincipal("agent-test-01");

        // Act
        await _sut.HandleAsync(request, caller, CancellationToken.None);

        // Assert
        await _agentRepo.Received(1).UpsertAsync(
            Arg.Is<AgentDocument>(a =>
                a.RegisteredAt == _timeProvider.GetUtcNow() &&
                a.LastHeartbeat == _timeProvider.GetUtcNow()),
            Arg.Any<CancellationToken>());
    }
}
```

**TDD Workflow:**

```
1. Write a failing BDD scenario (Gherkin)         → RED
2. Write a failing unit test for the handler       → RED
3. Implement just enough production code           → GREEN
4. Refactor while keeping tests green              → REFACTOR
5. Repeat for next behavior
```

#### 4.7.7 Frontend — Admin Dashboard (Optional)

A lightweight admin dashboard for observing the AgentBus system. Not required for agent communication — only for human operators.

**Stack:**

| Technology | Version | Purpose |
|---|---|---|
| React | 19 | UI framework |
| TypeScript | 5.x | Type safety |
| Vite | 6.x | Build tooling |
| TanStack Query | 5.x | Server state management |
| Tailwind CSS | 4.x | Styling |

**Dashboard Views:**
- **Agent Registry** — list registered agents, status, capabilities, last heartbeat
- **Message Monitor** — recent messages, DLQ depth, throughput sparklines
- **Event Stream** — live event feed, subscription topology
- **System Health** — Service Bus / Cosmos DB status, alert summary

**Deployment:** Static site in Azure Storage + CDN, or built into the Container App as a static file middleware.

---

## 5. Infrastructure & Deployment

### 5.1 Azure Resources (MVP)

```
Resource Group: rg-agentbus-mvp
├── Azure Container Apps Environment
│   └── Container App: agentbus-broker-api
├── Azure Service Bus Namespace (Standard)
│   ├── Queues: agent-{id}-inbox (dynamic)
│   └── Topics: events.{domain}.{type} (dynamic)
├── Azure Cosmos DB Account (Serverless, NoSQL API)
│   └── Database: agentbus
│       └── Container: agents (partition key: /partitionKey)
├── Azure Application Insights
├── Azure Container Registry
├── User-Assigned Managed Identities
│   ├── mi-agentbus-broker (for the Broker API)
│   └── mi-agent-{name} (one per agent)
└── Entra ID App Registration: AgentBus API
    └── App Roles: AgentBus.Agent, AgentBus.Admin
```

### 5.2 Infrastructure as Code

**MVP:** Bicep templates (upgradeable to Terraform for multi-cloud)

```
infra/
├── main.bicep                  # Orchestrator
├── modules/
│   ├── container-apps.bicep    # Container Apps Environment + App
│   ├── service-bus.bicep       # Service Bus Namespace
│   ├── cosmos-db.bicep         # Cosmos DB Account + Database
│   ├── monitoring.bicep        # App Insights + Log Analytics
│   ├── identity.bicep          # Managed Identities + Role Assignments
│   └── registry.bicep          # Container Registry
└── parameters/
    ├── dev.bicepparam
    └── prod.bicepparam
```

### 5.3 CI/CD

**MVP:** GitHub Actions

```yaml
# Simplified pipeline
trigger: push to main
steps:
  1. Build & test .NET solution
  2. Build container image → push to ACR
  3. Deploy Bicep infrastructure (what-if → deploy)
  4. Deploy Container App revision
  5. Run smoke tests against /health
```

**Enterprise Upgrade Path:**
- Azure DevOps with environment approvals and gates
- Blue/green deployments with traffic splitting
- Infrastructure drift detection
- Automated load testing in staging

---

## 6. WAF Pillar Assessment

### Security
| Aspect | Implementation |
|---|---|
| **Identity** | Managed Identity — zero secrets, no credential rotation needed |
| **AuthN** | Entra ID JWT validation on every API call |
| **AuthZ** | App Roles (RBAC) on the Broker API app registration |
| **Network** | MVP: public endpoint with Entra authN. Enterprise: Private Endpoints + VNET integration |
| **Data** | Cosmos DB encryption at rest (platform-managed keys). Service Bus TLS in transit |
| **Governance** | Azure Policy for resource tagging and SKU constraints |

### Reliability
| Aspect | Implementation |
|---|---|
| **Messaging** | Service Bus provides durable, triple-redundant message storage |
| **Dead Letters** | DLQ for failed messages — no silent data loss |
| **Registry** | Cosmos DB SLA with AZ-redundancy on serverless |
| **Heartbeats** | Agent liveness detection; stale agents marked inactive |
| **Retry** | Exponential backoff on Service Bus operations (SDK built-in) |

### Performance Efficiency
| Aspect | Implementation |
|---|---|
| **Serverless** | Container Apps auto-scales to zero; Cosmos DB serverless = pay per request |
| **Async** | All message delivery is async — no blocking between agents |
| **Partitioning** | Cosmos DB partition key per agent type (for discovery queries) |
| **Caching** | MVP: none. Enterprise: Redis cache for hot registry lookups |

### Cost Optimization
| Aspect | Implementation |
|---|---|
| **Serverless-first** | Cosmos DB serverless + Container Apps consumption = near-zero idle cost |
| **Service Bus Standard** | ~$0.0135/10K operations; sufficient for MVP volumes |
| **No over-provisioning** | All services are pay-per-use at MVP tier |
| **Estimated MVP cost** | < $50/month at low traffic |

### Operational Excellence
| Aspect | Implementation |
|---|---|
| **IaC** | Bicep for all infrastructure — repeatable, version-controlled |
| **CI/CD** | GitHub Actions — automated build, test, deploy |
| **Observability** | Distributed tracing + structured logging from day 1 |
| **API Versioning** | `/api/v1/` prefix — breaking changes get new version |

---

## 7. MVP → Enterprise Upgrade Matrix

```
MVP Component                    Enterprise Component
─────────────────────────────    ───────────────────────────────────
Container Apps (Consumption)  →  Container Apps (Dedicated) + APIM
Service Bus Standard          →  Service Bus Premium (VNET, sessions)
Cosmos DB Serverless          →  Cosmos DB Provisioned + Geo-Repl.
App Insights                  →  Azure Monitor full stack + Grafana
Public endpoints + Entra      →  Private Endpoints + VNET + NSGs
Single region                 →  Multi-region active-passive
GitHub Actions                →  Azure DevOps + env gates
Bicep                         →  Bicep or Terraform (multi-cloud)
Basic RBAC (2 roles)          →  Fine-grained RBAC + Cond. Access
```

---

## 8. Agent SDK Contract (Conceptual)

To make agent onboarding frictionless, provide a thin SDK:

```csharp
// Agent SDK usage example
var agentBus = new AgentBusClient(new AgentBusOptions
{
    BrokerUrl = "https://agentbus-broker.azurecontainerapps.io",
    // Uses DefaultAzureCredential — picks up Managed Identity automatically
});

// Register this agent
await agentBus.RegisterAsync(new AgentRegistration
{
    Name = "DroneScheduler",
    Version = "1.0.0",
    Capabilities = [new("schedule-drone", "Schedule a drone for delivery")],
    Accepts = ["schedule-drone-request"],
    Emits = ["drone-scheduled", "drone-in-transit"]
});

// Send direct message
await agentBus.SendAsync("agent-package-service-01", new Message
{
    Type = "schedule-drone-request",
    Payload = new { PackageId = "pkg-123", Destination = new { Lat = 47.6, Lng = -122.3 } }
});

// Subscribe to events
await agentBus.SubscribeAsync("events.logistics.package-ready", async (evt) =>
{
    var data = evt.GetPayload<PackageReadyEvent>();
    // Process event...
    await evt.AcknowledgeAsync();
});

// Receive direct messages
await agentBus.ReceiveAsync(async (msg) =>
{
    var request = msg.GetPayload<ScheduleDroneRequest>();
    // Process and optionally reply
    await msg.ReplyAsync(new { DroneId = "drone-42", ETA = DateTime.UtcNow.AddHours(1) });
});
```

---

## 9. Sequence: Full Agent Lifecycle

```
                    Broker API            Cosmos DB         Service Bus
                        │                     │                  │
 ── REGISTRATION ──────────────────────────────────────────────────
Agent A starts up       │                     │                  │
  │  POST /agents       │                     │                  │
  │  {name, caps, MI}   │                     │                  │
  │────────────────────▶│                     │                  │
  │                     │  Upsert agent doc   │                  │
  │                     │────────────────────▶│                  │
  │                     │  Create inbox queue │                  │
  │                     │──────────────────────────────────────▶│
  │                     │  Publish system evt │                  │
  │                     │  "agent-registered" │                  │
  │                     │──────────────────────────────────────▶│
  │  201 Created        │                     │                  │
  │◀────────────────────│                     │                  │
  │                     │                     │                  │
 ── DISCOVERY ─────────────────────────────────────────────────────
Agent B looks up caps   │                     │                  │
  │  GET /agents/       │                     │                  │
  │  discover?cap=X     │                     │                  │
  │────────────────────▶│                     │                  │
  │                     │  Query by capability│                  │
  │                     │────────────────────▶│                  │
  │  [{agentA details}] │                     │                  │
  │◀────────────────────│                     │                  │
  │                     │                     │                  │
 ── DIRECT MESSAGE ────────────────────────────────────────────────
Agent B → Agent A       │                     │                  │
  │  POST /messages/    │                     │                  │
  │  send {to: A}       │                     │                  │
  │────────────────────▶│                     │                  │
  │                     │  Enqueue to A-inbox │                  │
  │                     │──────────────────────────────────────▶│
  │  202 Accepted       │                     │                  │
  │◀────────────────────│                     │                  │
  │                     │                     │                  │
Agent A receives        │                     │                  │
  │  GET /messages/     │                     │                  │
  │  receive            │                     │                  │
  │────────────────────▶│                     │                  │
  │                     │  Dequeue from inbox │                  │
  │                     │◀─────────────────────────────────────│
  │  {message payload}  │                     │                  │
  │◀────────────────────│                     │                  │
  │                     │                     │                  │
 ── EVENT PUB/SUB ─────────────────────────────────────────────────
Agent A publishes       │                     │                  │
  │  POST /events/      │                     │                  │
  │  publish            │                     │                  │
  │────────────────────▶│                     │                  │
  │                     │  Publish to topic   │                  │
  │                     │──────────────────────────────────────▶│
  │                     │                     │  Fan-out ───────▶│ Subscribers
  │  202 Accepted       │                     │                  │
  │◀────────────────────│                     │                  │
```

---

## 10. Key Design Decisions & Trade-offs

| Decision | Rationale | Trade-off |
|---|---|---|
| **Service Bus over Event Grid for MVP** | Unified service for both queues and topics; simpler to manage one namespace | Event Grid is better for massive fan-out and webhook push; can add later |
| **Cosmos DB Serverless** | Pay-per-request ideal for unpredictable MVP traffic; no capacity planning | Single region only; 1MB max document size; can't add regions later (must migrate to provisioned) |
| **User-Assigned Managed Identity** | Sharable across resources; independent lifecycle; recommended by Microsoft | Requires pre-provisioning the MI before deploying the agent |
| **Broker API as mediator** | Central point for auth, validation, and routing; simplifies agent SDK | Single point of potential failure; adds latency hop |
| **Dynamic queue/topic creation** | Zero-config for agents; just register and go | Must handle cleanup for deregistered agents; potential orphaned resources |
| **HTTP long-poll over WebSocket** | Simpler to implement and debug; works through all proxies | Higher latency than persistent connections; more chattier |
| **Modular Monolith over Microservices** | Single deployable unit = simpler ops, debugging, transactions; module boundaries enable future extraction | All modules share a process — a bug in one can affect others; mitigated by module isolation discipline |
| **Vertical Slices over Layered Architecture** | Each feature is self-contained; no shotgun surgery across layers; easy to onboard new developers | Some code duplication between slices (e.g., similar validation); acceptable trade-off for independence |
| **Reqnroll (Gherkin BDD) over plain integration tests** | Living documentation; stakeholder-readable specs; drives outside-in design | Extra ceremony for simple CRUD; justified for a system with complex agent interaction workflows |
| **.NET 9 / C# 13** | Latest LTS with primary constructors, collection expressions, perf improvements | Agents using older .NET must use the SDK (HTTP) — no direct framework dependency |

---

## 11. Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| **Broker API single point of failure** | All agent communication blocked | Container Apps auto-restart + multi-replica; enterprise: multi-region |
| **Orphaned queues/topics** | Resource leakage when agents deregister improperly | Background cleanup job based on heartbeat TTL |
| **Message poisoning** | Bad messages block agent processing | DLQ with alerting; max delivery count on queues |
| **Cosmos DB serverless cold start** | Higher latency on first request after idle | Acceptable for MVP; enterprise: provisioned throughput |
| **Service Bus namespace limits** | Max 10K queues per namespace (Standard) | Sufficient for MVP; enterprise: multiple namespaces |

---

## 12. Getting Started — Implementation Order

### Phase 1: Solution Scaffold & Foundation (Week 1-2)
1. Create .NET 9 solution with modular monolith structure (`Directory.Build.props`, `global.json`, `.editorconfig`)
2. Set up Shared Kernel project (domain primitives, `Result<T>`, abstractions)
3. Set up test projects: `AgentBus.Tests.Unit` (xUnit + Shouldly), `AgentBus.Tests.BDD` (Reqnroll + Gherkin)
4. Set up Bicep infrastructure (Cosmos DB, Service Bus, Container Apps, Identities)
5. Implement Broker API host with health endpoints and module registration pattern
6. Set up CI/CD pipeline (build → test → deploy)

### Phase 2: Registration Module — BDD/TDD (Week 2-3)
7. Write Gherkin BDD scenarios for RegisterAgent, DiscoverAgents, Heartbeat
8. TDD the RegisterAgent slice: handler → validator → Cosmos persistence → inbox provisioning
9. TDD the DiscoverAgents slice: capability query
10. TDD the Heartbeat + Deregister slices
11. Integration tests with `WebApplicationFactory`

### Phase 3: Messaging Module — BDD/TDD (Week 3-4)
12. Write Gherkin BDD scenarios for SendMessage, ReceiveMessage
13. TDD the SendMessage slice: validation → registry lookup → Service Bus enqueue
14. TDD the ReceiveMessage slice: inbox dequeue → long-poll
15. TDD the AcknowledgeMessage slice

### Phase 4: Eventing Module — BDD/TDD (Week 4-5)
16. Write Gherkin BDD scenarios for PublishEvent, Subscribe, ReceiveEvents
17. TDD the PublishEvent slice: topic routing → fan-out
18. TDD the Subscribe/Unsubscribe slices: subscription management
19. TDD the ReceiveEvents slice

### Phase 5: Identity, SDK & Polish (Week 5-6)
20. Integrate Managed Identity authentication on Broker API
21. Implement Agent SDK (C# NuGet package) with `DefaultAzureCredential`
22. Add observability (OpenTelemetry + App Insights)
23. End-to-end BDD scenarios with 2-3 sample agents
24. Optional: Admin dashboard (React + TypeScript)

---

## References

**Azure Services & Patterns:**
- [Azure Service Bus Messaging Overview](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-overview)
- [Publisher-Subscriber Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber)
- [Choreography Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/choreography)
- [Managed Identities for Azure Resources](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview)
- [Azure Cosmos DB Serverless](https://learn.microsoft.com/en-us/azure/cosmos-db/serverless)
- [Interservice Communication for Microservices](https://learn.microsoft.com/en-us/azure/architecture/microservices/design/interservice-communication)
- [Enterprise Integration with Queues and Events](https://learn.microsoft.com/en-us/azure/architecture/example-scenario/integration/queues-events)

**Software Architecture:**
- [Vertical Slice Architecture — Jimmy Bogard](https://www.jimmybogard.com/vertical-slice-architecture/)
- [Modular Monolith — Milan Jovanović](https://www.milanjovanovic.tech/blog/what-is-a-modular-monolith)
- [.NET 9 — What's New](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9)
- [C# 13 — What's New](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13)

**Testing:**
- [Reqnroll — BDD for .NET (Gherkin)](https://reqnroll.net/)
- [xUnit — .NET Testing Framework](https://xunit.net/)
- [Shouldly — Assertion Framework](https://docs.shouldly.org/)
- [NSubstitute — Mocking](https://nsubstitute.github.io/)
- [Bogus — Fake Data Generator](https://github.com/bchavez/Bogus)
