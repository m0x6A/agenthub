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
- Runtime: .NET 8 / ASP.NET Core Minimal APIs
- Serialization: System.Text.Json
- Service Bus SDK: Azure.Messaging.ServiceBus
- Cosmos DB SDK: Microsoft.Azure.Cosmos
- Auth: Microsoft.Identity.Web (Managed Identity validation)

**Enterprise Upgrade Path:**
- Azure API Management (APIM) front door for rate limiting, API versioning, developer portal
- Multi-region deployment with Azure Front Door
- gRPC support for high-performance inter-agent communication
- WebSocket support for real-time event streaming

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

### Phase 1: Foundation (Week 1-2)
1. Set up Bicep infrastructure (Cosmos DB, Service Bus, Container Apps, Identities)
2. Implement Broker API skeleton with health endpoints
3. Implement Agent Registry CRUD in Cosmos DB
4. Set up CI/CD pipeline

### Phase 2: Messaging (Week 3-4)
5. Implement direct message send/receive via Service Bus queues
6. Implement event publish/subscribe via Service Bus topics
7. Dynamic queue and subscription provisioning on agent registration

### Phase 3: Identity & Polish (Week 5-6)
8. Integrate Managed Identity authentication on Broker API
9. Implement Agent SDK (C# NuGet package)
10. Add observability (OpenTelemetry + App Insights)
11. End-to-end integration tests with 2-3 sample agents

---

## References

- [Azure Service Bus Messaging Overview](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-overview)
- [Publisher-Subscriber Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber)
- [Choreography Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/choreography)
- [Managed Identities for Azure Resources](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview)
- [Azure Cosmos DB Serverless](https://learn.microsoft.com/en-us/azure/cosmos-db/serverless)
- [Interservice Communication for Microservices](https://learn.microsoft.com/en-us/azure/architecture/microservices/design/interservice-communication)
- [Enterprise Integration with Queues and Events](https://learn.microsoft.com/en-us/azure/architecture/example-scenario/integration/queues-events)
