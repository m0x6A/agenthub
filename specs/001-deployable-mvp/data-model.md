# Data Model: AgentBus Deployable MVP

**Date**: 2026-02-11  
**Feature**: 001-deployable-mvp  
**Purpose**: Define domain entities, value objects, validation rules, and state transitions

## Overview

The AgentBus MVP data model consists of 5 core entities that support agent registration, discovery, direct messaging, and pub/sub eventing. All entities are designed as immutable records for thread safety and testability. Persistence uses Azure Cosmos DB (NoSQL API) for agent registry and subscriptions, while message/event envelopes flow through Azure Service Bus.

## Domain Entities

### 1. Agent

Represents an autonomous agent registered in the AgentBus broker system.

**Storage**: Cosmos DB container `agents`, partition key `/partitionKey` (value = agent ID for single-tenant optimization)

**Attributes**:
| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `id` | string | Yes | Unique, 3-64 chars, alphanumeric + hyphens | Agent unique identifier (e.g., "logistics-coordinator-v1-abc123") |
| `partitionKey` | string | Yes | Same as `id` | Cosmos DB partition key for point reads |
| `name` | string | Yes | 3-100 chars | Human-readable agent name (e.g., "Logistics Coordinator") |
| `version` | string | Yes | SemVer 2.0 format | Agent version (e.g., "1.0.0", "2.1.3-beta") |
| `status` | enum | Yes | "active" or "inactive" | Current operational status |
| `capabilities` | array | Yes | 1-50 items, each 3-100 chars | List of functional capabilities (e.g., ["schedule-drone", "assign-route"]) |
| `messageTypes` | object | Yes | Non-empty | Message types the agent accepts and emits |
| `messageTypes.accepts` | array | Yes | 0-50 items | Message type patterns agent can handle (e.g., ["*.logistics.route"]) |
| `messageTypes.emits` | array | Yes | 0-50 items | Message types agent publishes (e.g., ["logistics.route-assigned"]) |
| `identity` | object | Yes | Non-null | Azure identity information |
| `identity.managedIdentityId` | string | Yes | Azure resource ID | User-Assigned Managed Identity resource ID |
| `identity.principalId` | string | Yes | GUID | Managed Identity principal ID (from JWT sub claim) |
| `identity.tenantId` | string | Yes | GUID | Entra ID tenant ID |
| `endpoints` | object | Yes | Non-null | Agent communication endpoints |
| `endpoints.inboxQueueName` | string | Yes | Generated: `agent-{id}-inbox` | Service Bus queue name for direct messages |
| `endpoints.healthCheckUrl` | string | No | Valid HTTPS URL | Optional health check endpoint hosted by agent |
| `metadata` | object | No | Max 10 key-value pairs | Custom agent metadata |
| `metadata.owner` | string | No | 3-100 chars | Owner team or email |
| `metadata.environment` | string | No | Enum: "dev", "staging", "prod" | Deployment environment |
| `metadata.tags` | array | No | 0-20 items | Custom tags for grouping |
| `timestamps` | object | Yes | Non-null | Temporal tracking |
| `timestamps.registeredAt` | DateTime | Yes | ISO 8601 UTC | Initial registration timestamp |
| `timestamps.lastHeartbeat` | DateTime | Yes | ISO 8601 UTC | Last heartbeat update timestamp |

**Validation Rules**:
- `id` MUST be unique across all agents (Cosmos DB unique key constraint)
- `capabilities` array MUST NOT contain duplicates
- `version` MUST match SemVer 2.0 regex: `^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[a-zA-Z0-9\-.]+)?(\+[a-zA-Z0-9\-.]+)?$`
- `identity.principalId` MUST match JWT `sub` claim on registration
- `timestamps.lastHeartbeat` MUST NOT be in the future

**State Transitions**:
```
[Initial] → RegisterAgent → [Active]
[Active] → UpdateHeartbeat → [Active] (lastHeartbeat updated)
[Active] → MissedHeartbeat (>5 min) → [Inactive]
[Inactive] → UpdateHeartbeat → [Active] (reactivation)
[Active|Inactive] → DeregisterAgent → [Deleted]
```

**Example JSON**:
```json
{
  "id": "logistics-coordinator-v1-abc123",
  "partitionKey": "logistics-coordinator-v1-abc123",
  "name": "Logistics Coordinator",
  "version": "1.0.0",
  "status": "active",
  "capabilities": ["schedule-drone", "assign-route", "calculate-eta"],
  "messageTypes": {
    "accepts": ["command.logistics.*", "query.route.*"],
    "emits": ["event.logistics.route-assigned", "event.logistics.drone-scheduled"]
  },
  "identity": {
    "managedIdentityId": "/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/agent-logistics",
    "principalId": "12345678-1234-1234-1234-123456789abc",
    "tenantId": "87654321-4321-4321-4321-cba987654321"
  },
  "endpoints": {
    "inboxQueueName": "agent-logistics-coordinator-v1-abc123-inbox",
    "healthCheckUrl": "https://logistics-agent.example.com/health"
  },
  "metadata": {
    "owner": "[email protected]",
    "environment": "prod",
    "tags": ["logistics", "critical-path"]
  },
  "timestamps": {
    "registeredAt": "2026-02-11T10:30:00Z",
    "lastHeartbeat": "2026-02-11T10:35:42Z"
  }
}
```

**C# Record Definition**:
```csharp
public sealed record Agent(
    string Id,
    string PartitionKey,
    string Name,
    string Version,
    AgentStatus Status,
    string[] Capabilities,
    MessageTypes MessageTypes,
    AgentIdentity Identity,
    AgentEndpoints Endpoints,
    AgentMetadata? Metadata,
    AgentTimestamps Timestamps);

public enum AgentStatus { Active, Inactive }

public sealed record MessageTypes(string[] Accepts, string[] Emits);

public sealed record AgentIdentity(
    string ManagedIdentityId,
    string PrincipalId,
    string TenantId);

public sealed record AgentEndpoints(
    string InboxQueueName,
    string? HealthCheckUrl);

public sealed record AgentMetadata(
    string? Owner,
    string? Environment,
    string[]? Tags);

public sealed record AgentTimestamps(
    DateTime RegisteredAt,
    DateTime LastHeartbeat);
```


### 2. Message Envelope

Wraps direct point-to-point messages sent between agents via Service Bus queues.

**Storage**: Azure Service Bus queues (one queue per agent: `agent-{id}-inbox`), ephemeral (deleted after acknowledgment)

**Attributes**:
| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `messageId` | string | Yes | GUID | Unique message identifier |
| `correlationId` | string | No | GUID | Correlation ID for request/response pairing |
| `conversationId` | string | No | GUID | Conversation thread identifier for multi-turn interactions |
| `from` | string | Yes | Valid agent ID | Sender agent ID (validated against registry) |
| `to` | string | Yes | Valid agent ID | Receiver agent ID (validated against registry) |
| `messageType` | string | Yes | 3-100 chars | Semantic message type (e.g., "command.logistics.schedule-drone") |
| `timestamp` | DateTime | Yes | ISO 8601 UTC | Message creation timestamp |
| `ttl` | TimeSpan | No | Default: 24 hours | Time-to-live before expiration (max 14 days) |
| `payload` | object | Yes | Max 200 KB JSON | Business payload (JSON serializable) |
| `replyTo` | string | No | Queue name | Optional reply-to queue name (defaults to sender inbox) |
| `headers` | object | No | Max 10 key-value pairs | Custom headers for routing/filtering |
| `headers.x-trace-id` | string | No | W3C TraceContext | Distributed trace ID for correlation |
| `headers.x-agent-version` | string | No | SemVer | Sender agent version |

**Validation Rules**:
- `from` MUST be an active agent in registry
- `to` MUST exist in agent registry (can be inactive)
- `payload` serialized size MUST be ≤ 256 KB (Service Bus Standard limit)
- `ttl` MUST be ≤ 14 days (Service Bus maximum)
- `messageType` SHOULD match receiver's `messageTypes.accepts` patterns (warning, not enforced)

**Service Bus Mapping**:
- Envelope serialized as JSON in `ServiceBusMessage.Body`
- `messageId` → `ServiceBusMessage.MessageId`
- `correlationId` → `ServiceBusMessage.CorrelationId`
- `headers` → `ServiceBusMessage.ApplicationProperties`

**Example JSON**:
```json
{
  "messageId": "msg-550e8400-e29b-41d4-a716-446655440000",
  "correlationId": "corr-12345678-90ab-cdef-1234-567890abcdef",
  "conversationId": "conv-abc-xyz-123",
  "from": "weather-agent-v2",
  "to": "logistics-coordinator-v1-abc123",
  "messageType": "command.logistics.schedule-drone",
  "timestamp": "2026-02-11T10:40:15Z",
  "ttl": "PT24H",
  "payload": {
    "droneId": "drone-42",
    "destination": {"lat": 37.7749, "lon": -122.4194},
    "priority": "high"
  },
  "replyTo": "agent-weather-agent-v2-inbox",
  "headers": {
    "x-trace-id": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    "x-agent-version": "2.1.0"
  }
}
```

**C# Record Definition**:
```csharp
public sealed record MessageEnvelope(
    string MessageId,
    string? CorrelationId,
    string? ConversationId,
    string From,
    string To,
    string MessageType,
    DateTime Timestamp,
    TimeSpan? Ttl,
    JsonElement Payload, // Flexible JSON payload
    string? ReplyTo,
    Dictionary<string, string>? Headers);
```


### 3. Event Envelope

Wraps published domain events for pub/sub communication via Service Bus topics.

**Storage**: Azure Service Bus topics (one topic per event type: `events.{domain}.{event-type}`), ephemeral

**Attributes**:
| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `eventId` | string | Yes | GUID | Unique event identifier |
| `eventType` | string | Yes | Topic name format: `events.{domain}.{event-type}` | Hierarchical event type (e.g., "events.logistics.drone-scheduled") |
| `source` | string | Yes | Valid agent ID | Publisher agent ID |
| `timestamp` | DateTime | Yes | ISO 8601 UTC | Event creation timestamp |
| `dataVersion` | string | Yes | SemVer | Event schema version for evolution |
| `data` | object | Yes | Max 200 KB JSON | Event payload (JSON serializable) |
| `headers` | object | No | Max 10 key-value pairs | Custom headers for filtering |
| `headers.x-trace-id` | string | No | W3C TraceContext | Distributed trace ID |
| `headers.x-correlation-id` | string | No | GUID | Correlation ID from originating message |

**Validation Rules**:
- `source` MUST be an active agent in registry
- `eventType` MUST match pattern: `^events\.[a-z0-9-]+\.[a-z0-9-]+$`
- `data` serialized size MUST be ≤ 256 KB
- `dataVersion` MUST be valid SemVer

**Service Bus Mapping**:
- Envelope serialized as JSON in `ServiceBusMessage.Body`
- `eventType` → Service Bus topic name
- `headers` → `ServiceBusMessage.ApplicationProperties`
- Topic auto-provisioned on first publish if not exists

**Example JSON**:
```json
{
  "eventId": "evt-7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "eventType": "events.logistics.drone-scheduled",
  "source": "logistics-coordinator-v1-abc123",
  "timestamp": "2026-02-11T10:45:00Z",
  "dataVersion": "1.0.0",
  "data": {
    "droneId": "drone-42",
    "flightPlan": {
      "origin": {"lat": 37.7749, "lon": -122.4194},
      "destination": {"lat": 34.0522, "lon": -118.2437},
      "estimatedDuration": "PT2H30M"
    },
    "scheduledTime": "2026-02-11T12:00:00Z"
  },
  "headers": {
    "x-trace-id": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    "x-correlation-id": "corr-12345678-90ab-cdef-1234-567890abcdef"
  }
}
```

**C# Record Definition**:
```csharp
public sealed record EventEnvelope(
    string EventId,
    string EventType,
    string Source,
    DateTime Timestamp,
    string DataVersion,
    JsonElement Data,
    Dictionary<string, string>? Headers);
```


### 4. Capability

Describes a functional capability that an agent provides. Capabilities are stored as strings within the Agent entity but can be structured for schema validation in enterprise upgrades.

**Storage**: Embedded array within `Agent.capabilities` field

**Attributes**:
| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `name` | string | Yes | 3-100 chars, lowercase, hyphens | Capability identifier (e.g., "schedule-drone") |

**Validation Rules**:
- `name` MUST match pattern: `^[a-z0-9]+(-[a-z0-9]+)*$`
- `name` MUST be unique within an agent's capabilities array

**Enterprise Upgrade Path** (Out of Scope for MVP):
In post-MVP, capabilities can be expanded to full entities with:
- `description`: Human-readable description
- `inputSchema`: JSON Schema for request validation
- `outputSchema`: JSON Schema for response validation
- `version`: Capability version for evolution

**Example (MVP - Simple String)**:
```json
"capabilities": ["schedule-drone", "assign-route", "calculate-eta"]
```

**Example (Enterprise - Structured Object)**:
```json
"capabilities": [
  {
    "name": "schedule-drone",
    "description": "Assigns a drone to a delivery route",
    "version": "1.0.0",
    "inputSchema": {"$ref": "https://schema.example.com/schedule-drone-input-v1.json"},
    "outputSchema": {"$ref": "https://schema.example.com/schedule-drone-output-v1.json"}
  }
]
```


### 5. Subscription

Represents an agent's subscription to a specific event type via Service Bus topic subscription.

**Storage**: Cosmos DB container `subscriptions`, partition key `/agentId` (all subscriptions for an agent in same partition)

**Attributes**:
| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `id` | string | Yes | GUID | Subscription unique identifier |
| `partitionKey` | string | Yes | Agent ID | Cosmos DB partition key (subscriber agent ID) |
| `agentId` | string | Yes | Valid agent ID | Subscriber agent ID |
| `eventType` | string | Yes | Topic name format | Event type subscribed to (e.g., "events.logistics.drone-scheduled") |
| `serviceBusSubscriptionName` | string | Yes | Generated: `sub-{agentId}-{hash}` | Service Bus subscription name (max 50 chars) |
| `filters` | array | No | Max 10 SQL filter rules | Service Bus SQL filter rules for message filtering |
| `createdAt` | DateTime | Yes | ISO 8601 UTC | Subscription creation timestamp |

**Validation Rules**:
- `agentId` MUST be an active agent in registry
- `eventType` MUST match pattern: `^events\.[a-z0-9-]+\.[a-z0-9-]+$`
- `serviceBusSubscriptionName` MUST be ≤ 50 chars (Service Bus limit)
- `filters` SQL syntax MUST be valid per Service Bus SQL filter rules

**Service Bus Mapping**:
- Topic name = `eventType`
- Subscription name = `serviceBusSubscriptionName`
- Each subscription has dedicated Service Bus subscription (no sharing between agents for isolation)

**Example JSON**:
```json
{
  "id": "sub-f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "partitionKey": "logistics-coordinator-v1-abc123",
  "agentId": "logistics-coordinator-v1-abc123",
  "eventType": "events.weather.alert-issued",
  "serviceBusSubscriptionName": "sub-logistics-coordinator-a3b2c1",
  "filters": [
    "severity='high'",
    "region IN ('west-coast', 'central')"
  ],
  "createdAt": "2026-02-11T11:00:00Z"
}
```

**C# Record Definition**:
```csharp
public sealed record Subscription(
    string Id,
    string PartitionKey,
    string AgentId,
    string EventType,
    string ServiceBusSubscriptionName,
    string[]? Filters,
    DateTime CreatedAt);
```


## Cosmos DB Schema Design

### Container: `agents`

**Partition Key**: `/partitionKey` (string, value = agent ID)

**Rationale**: Each agent reads/writes its own document most frequently (heartbeats, registration updates). Using agent ID as partition key ensures single-partition operations (1 RU point reads).

**Indexing Policy**:
```json
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [
    {"path": "/capabilities/*"},
    {"path": "/status/?"},
    {"path": "/metadata/environment/?"},
    {"path": "/metadata/tags/*"}
  ],
  "excludedPaths": [
    {"path": "/identity/*"},
    {"path": "/timestamps/*"},
    {"path": "/endpoints/*"}
  ]
}
```

**Unique Key Constraints**:
- `/id` (unique across all partitions)

**TTL**: Disabled (agents persist until explicit deregistration)


### Container: `subscriptions`

**Partition Key**: `/partitionKey` (string, value = agent ID)

**Rationale**: All subscriptions for an agent can be retrieved in single partition query. Agent-scoped operations (list all my subscriptions, delete all on deregister) are efficient.

**Indexing Policy**:
```json
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [
    {"path": "/eventType/?"},
    {"path": "/agentId/?"}
  ],
  "excludedPaths": [
    {"path": "/serviceBusSubscriptionName/?"},
    {"path": "/createdAt/?"}
  ]
}
```

**Unique Key Constraints**:
- `/agentId` + `/eventType` (agent can only have one subscription per event type)

**TTL**: Disabled (subscriptions persist until explicit unsubscribe or agent deregistration)


## Service Bus Schema Design

### Queues

**Naming Convention**: `agent-{agentId}-inbox`

**Configuration**:
- Max size: 1 GB (Standard tier)
- Message TTL: 24 hours (configurable per message)
- Max delivery count: 10 (then move to DLQ)
- Dead-letter queue: Enabled (auto-created)
- Duplicate detection: Enabled (10-minute window)
- Sessions: Disabled (not required for MVP)

**RBAC**:
- Broker API Managed Identity: `Azure Service Bus Data Owner` (create/delete queues, send/receive)
- Agent Managed Identities: `Azure Service Bus Data Receiver` (receive from own inbox only)


### Topics

**Naming Convention**: `events.{domain}.{event-type}` (e.g., `events.logistics.drone-scheduled`)

**Configuration**:
- Max size: 1 GB
- Message TTL: 24 hours
- Duplicate detection: Enabled (10-minute window)
- Auto-delete on idle: Disabled

**Subscriptions** (per agent):
- Name: `sub-{agentId}-{hash}` (hash = first 8 chars of subscription ID)
- Max delivery count: 10
- Dead-letter on filter evaluation exceptions: Enabled
- Dead-letter on message expiration: Enabled

**RBAC**:
- Broker API Managed Identity: `Azure Service Bus Data Owner`
- Agent Managed Identities: `Azure Service Bus Data Sender` (publish to any topic)


## Validation Rules Summary

### Cross-Entity Validation

1. **Agent Registration**: `identity.principalId` must match JWT `sub` claim
2. **Message Sending**: `from` must match authenticated agent principal ID
3. **Message Sending**: `to` must exist in agent registry
4. **Event Publishing**: `source` must match authenticated agent principal ID
5. **Event Subscribing**: `agentId` must match authenticated agent principal ID
6. **Agent Deregistration**: Must delete all agent's subscriptions and inbox queue

### Data Integrity Rules

1. **Unique Agent IDs**: Cosmos DB unique key constraint on `/id`
2. **Unique Subscription per Event**: Unique key constraint on `/agentId` + `/eventType`
3. **Referential Integrity**: Message `to`/`from` and Event `source` validated against agent registry before processing
4. **Idempotency**: Service Bus duplicate detection prevents duplicate messages within 10-minute window


## State Transition Diagrams

### Agent Lifecycle

```
            ┌──────────────┐
            │   Initial    │
            └──────┬───────┘
                   │
         RegisterAgent (201 Created)
                   │
                   ▼
            ┌──────────────┐
     ┌──────┤    Active    ├──────┐
     │      └──────┬───────┘      │
     │             │              │
     │   UpdateHeartbeat     MissedHeartbeat
     │             │           (>5 min)
     │             │              │
     │             ▼              ▼
     │      ┌──────────────┐ ┌──────────────┐
     │      │   Active     │ │  Inactive    │
     │      └──────────────┘ └──────┬───────┘
     │                              │
     │                      UpdateHeartbeat
     │                        (reactivate)
     │                              │
     └──────────────────────────────┘
                   │
            DeregisterAgent (204 No Content)
                   │
                   ▼
            ┌──────────────┐
            │   Deleted    │
            └──────────────┘
```

### Message Lifecycle

```
            ┌──────────────┐
            │   Created    │
            └──────┬───────┘
                   │
         SendMessage (202 Accepted)
                   │
                   ▼
            ┌──────────────┐
            │  In Transit  │──────┐ TTL Expired
            └──────┬───────┘      │
                   │              ▼
         ReceiveMessage    ┌──────────────┐
                   │       │  Expired     │
                   ▼       │  (DLQ)       │
            ┌──────────────┐└──────────────┘
     ┌──────┤ Received     │
     │      └──────┬───────┘
     │             │
     │   AcknowledgeMessage
     │             │
     │             ▼
     │      ┌──────────────┐
     │      │ Acknowledged │
     │      └──────────────┘
     │
     └─────── Max Delivery Count (10) ────────┐
                                              ▼
                                       ┌──────────────┐
                                       │   Failed     │
                                       │   (DLQ)      │
                                       └──────────────┘
```


## Performance Considerations

### Cosmos DB Request Units

| Operation | Partition Key | Index | Estimated RU/s |
|-----------|---------------|-------|----------------|
| Point read agent by ID | Yes | N/A | 1 RU |
| Query agents by capability | No (cross-partition) | Indexed | 3-10 RU |
| Upsert agent (registration) | Yes | All properties | 5-10 RU |
| Update heartbeat (patch) | Yes | Timestamp only | 2-3 RU |
| Query subscriptions by agent | Yes | N/A | 2-5 RU |

**Optimization Strategy**: Use point reads with partition key wherever possible. Capability discovery query is cross-partition but infrequent (cached by agents).


### Service Bus Throughput

| Operation | Billed Operations | Latency Target |
|-----------|-------------------|----------------|
| Send message to queue | 1 op | < 100ms P95 |
| Receive message (long-poll) | 1 op per receive call (not per message) | < 1s P95 |
| Publish event to topic | 1 op | < 100ms P95 |
| Receive event from subscription | 1 op per receive call | < 1s P95 |

**Optimization Strategy**: Use long-polling (`maxWaitTime: 30s`) to minimize billable receive operations. Batch message sends when possible (up to 100 messages per `SendMessageBatchAsync`).


## Security Considerations

### Row-Level Security

**Agents Container**: No RLS required - agents can only read their own document (partition key = agent ID) or perform cross-partition discovery queries (read-only).

**Subscriptions Container**: Agents can only query subscriptions where `partitionKey = authenticated agent ID`. Cosmos DB does not enforce RLS natively; enforcement is in application logic.

**Service Bus Queues**: RBAC limits agents to `Azure Service Bus Data Receiver` on their own inbox queue only (enforced by Azure RBAC).

### Data Encryption

- **At Rest**: Cosmos DB and Service Bus encrypt all data with Microsoft-managed keys (default)
- **In Transit**: All connections use TLS 1.2+ (HTTPS for Container Apps, AMQPS for Service Bus)
- **Enterprise Upgrade**: Customer-managed keys via Azure Key Vault for CMEK compliance


## API Contracts Preview

See [contracts/](./contracts/) directory for OpenAPI 3.1 specifications generated from this data model.

**Key Endpoints**:
- `POST /api/v1/agents` → Accepts `Agent` DTO (subset), returns `Agent` with generated fields
- `GET /api/v1/agents?capability={name}` → Returns `Agent[]`
- `POST /api/v1/messages/send` → Accepts `MessageEnvelope`, returns `{ messageId }`
- `GET /api/v1/messages/receive` → Returns `MessageEnvelope?`
- `POST /api/v1/events/publish` → Accepts `EventEnvelope`, returns `{ eventId }`
- `POST /api/v1/events/subscribe` → Accepts `{ eventType, filters? }`, returns `Subscription`
