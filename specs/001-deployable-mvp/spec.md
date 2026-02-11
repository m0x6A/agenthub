# Feature Specification: AgentBus Deployable MVP

**Feature Branch**: `001-deployable-mvp`  
**Created**: 2026-02-11  
**Status**: Draft  
**Input**: User description: "Create a deployable MVP for ARCHITECTURE.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Agent Registration & Discovery (Priority: P1)

An autonomous agent needs to register itself with the AgentBus broker, declare its capabilities, and discover other agents by their capabilities. This is the foundation for all agent-to-agent communication.

**Why this priority**: Without registration and discovery, no agent communication can occur. This is the core service discovery pattern that enables the entire platform.

**Independent Test**: Deploy a single agent, have it register with the broker, query for its own registration, and discover itself by capability. System delivers value by providing a working agent registry.

**Acceptance Scenarios**:

1. **Given** I am a new agent with Managed Identity credentials, **When** I send a POST request to `/api/v1/agents` with my name, version, capabilities, and message types, **Then** I receive a 201 Created response with my agent ID and inbox queue name, and I can retrieve my registration via GET `/api/v1/agents/{myId}`

2. **Given** I am a registered agent, **When** I send periodic heartbeat requests via PATCH `/api/v1/agents/{myId}/heartbeat`, **Then** my `lastHeartbeat` timestamp is updated in the registry and my status remains "active"

3. **Given** multiple agents are registered with different capabilities, **When** I query GET `/api/v1/agents?capability=schedule-drone`, **Then** I receive a list of all agents that have the "schedule-drone" capability

4. **Given** I am a registered agent, **When** I send a DELETE request to `/api/v1/agents/{myId}`, **Then** my registration is removed from the registry and my inbox queue is deleted

5. **Given** I am an unauthenticated caller without Managed Identity, **When** I attempt to register, **Then** I receive a 401 Unauthorized response

---

### User Story 2 - Direct Agent-to-Agent Messaging (Priority: P2)

Agents need to send direct, point-to-point messages to specific agents they've discovered. Each agent has a dedicated inbox queue for receiving messages.

**Why this priority**: Direct messaging is the primary value proposition for synchronized agent collaboration. It enables request/response patterns between agents.

**Independent Test**: Register two agents (A and B), have agent A send a message to agent B's inbox, agent B retrieves the message, and acknowledges processing. System delivers value by enabling reliable point-to-point communication.

**Acceptance Scenarios**:

1. **Given** I am agent A and agent B is registered, **When** I POST a message to `/api/v1/messages/send` with `to: agent-b-id` and a payload, **Then** I receive a 202 Accepted response and the message is enqueued in agent B's inbox queue (`agent-b-id-inbox`)

2. **Given** I am agent B with messages in my inbox, **When** I send a GET request to `/api/v1/messages/receive` with long-polling, **Then** I receive the oldest unprocessed message with full envelope (messageId, from, payload, timestamp, trace-id)

3. **Given** I am agent B and have received a message, **When** I POST to `/api/v1/messages/{messageId}/ack`, **Then** the message is removed from my inbox and marked as processed

4. **Given** I am agent A sending to a non-existent agent ID, **When** I POST a message, **Then** I receive a 404 Not Found response indicating the target agent doesn't exist

5. **Given** I am agent A sending a message that exceeds Service Bus size limits, **When** I POST the message, **Then** I receive a 413 Payload Too Large response

---

### User Story 3 - Event Publishing & Subscription (Priority: P3)

Agents need to publish domain events to well-known topics and subscribe to events from other agents. Multiple agents can subscribe to the same event type for fan-out scenarios.

**Why this priority**: Event-driven architecture enables loose coupling and reactive patterns. Agents can broadcast state changes without knowing who will consume them.

**Independent Test**: Register two agents, agent A subscribes to event type "drone-scheduled", agent B publishes a "drone-scheduled" event, agent A receives the event. System delivers value by enabling pub/sub communication patterns.

**Acceptance Scenarios**:

1. **Given** I am agent A, **When** I POST to `/api/v1/events/subscribe` with `eventType: "events.logistics.drone-scheduled"`, **Then** I receive a subscription ID and a Service Bus subscription is created on the topic for my agent

2. **Given** I am agent B, **When** I POST to `/api/v1/events/publish` with event type "drone-scheduled" and event data, **Then** I receive a 202 Accepted response and the event is published to the `events.logistics.drone-scheduled` topic

3. **Given** I am agent A with an active subscription, **When** agent B publishes an event I'm subscribed to, **Then** I can GET `/api/v1/events/receive/{subscriptionId}` and retrieve the event with full envelope (eventId, eventType, source, data, timestamp)

4. **Given** I am agent A with a subscription, **When** I send DELETE `/api/v1/events/subscriptions/{subId}`, **Then** my subscription is removed and I stop receiving events of that type

5. **Given** multiple agents (A, B, C) are subscribed to the same event type, **When** an agent publishes that event, **Then** all subscribers receive a copy (fan-out)

---

### User Story 4 - Infrastructure Deployment & Configuration (Priority: P4)

DevOps teams need to deploy the entire AgentBus infrastructure to Azure using Infrastructure as Code, with all resources properly configured and connected.

**Why this priority**: A deployable MVP requires repeatable, automated infrastructure provisioning. Manual setup is error-prone and not production-ready.

**Independent Test**: Run Bicep deployment script against a clean Azure subscription, verify all resources are created with proper configuration, run health checks. System delivers value by providing production-ready infrastructure.

**Acceptance Scenarios**:

1. **Given** I have an Azure subscription and Azure CLI installed, **When** I run `az deployment sub create` with the main.bicep template, **Then** all resources (Container Apps, Service Bus, Cosmos DB, App Insights, Managed Identities, Container Registry) are created in a new resource group

2. **Given** infrastructure is deployed, **When** I check the Broker API health endpoint at `https://<broker-url>/api/v1/health`, **Then** I receive a 200 OK response with status "healthy" and all dependency checks passing

3. **Given** infrastructure is deployed, **When** I inspect the Cosmos DB container, **Then** I see the "agents" container with partition key `/partitionKey` configured for serverless throughput

4. **Given** infrastructure is deployed, **When** I inspect App Insights, **Then** I see distributed tracing enabled with OpenTelemetry instrumentation and correlation IDs configured

5. **Given** infrastructure is deployed, **When** I inspect Entra ID App Registration, **Then** I see "AgentBus.Agent" and "AgentBus.Admin" app roles defined for RBAC

---

### User Story 5 - Observability & Monitoring (Priority: P5)

Operations teams need full visibility into agent registrations, message flows, event patterns, and system health through distributed tracing and structured logging.

**Why this priority**: Production systems require observability for debugging, performance tuning, and incident response. This is essential for operational excellence.

**Independent Test**: Execute a full agent workflow (register, send message, publish event), query App Insights for traces and logs, verify correlation. System delivers value by providing production debugging capabilities.

**Acceptance Scenarios**:

1. **Given** I am making API calls to the Broker API, **When** I include an `x-trace-id` header, **Then** all logs, traces, and Service Bus operations are correlated with that trace ID in Application Insights

2. **Given** agents are registering and communicating, **When** I query Application Insights custom metrics, **Then** I see metrics for: agents_registered_total, messages_sent_per_second, events_published_per_second, dlq_depth

3. **Given** a message fails processing and goes to dead-letter queue, **When** I check Application Insights alerts, **Then** I receive an alert notification for "DLQ depth > 0"

4. **Given** an agent misses heartbeats for 5 minutes, **When** the heartbeat check runs, **Then** the agent's status is updated to "inactive" and an alert is triggered

5. **Given** I am debugging a failed message delivery, **When** I query Application Insights with the trace ID, **Then** I see the complete flow: API request → Cosmos DB lookup → Service Bus enqueue → delivery attempt → error

---

### Edge Cases

- **What happens when an agent registers with duplicate ID?** System returns 409 Conflict and requires the agent to use a unique ID or deregister first
- **What happens when Service Bus topic doesn't exist for an event type?** Broker API auto-provisions the topic on first publish or subscribe
- **What happens when Cosmos DB is temporarily unavailable?** API returns 503 Service Unavailable with retry-after header; agent retries with exponential backoff
- **What happens when an agent's inbox queue reaches max size (80GB)?** New messages return 507 Insufficient Storage; oldest messages may be auto-forwarded to DLQ
- **What happens when two agents try to register simultaneously with same ID?** Cosmos DB's atomic upsert ensures only one succeeds; the other receives optimistic concurrency conflict
- **What happens when message TTL expires before processing?** Service Bus automatically moves expired messages to DLQ; agent can query DLQ for expired messages
- **How does system handle malformed JWT tokens?** Microsoft.Identity.Web middleware rejects with 401 Unauthorized before reaching application code
- **What happens when agent heartbeat timestamp is too old?** Background job marks agent as "inactive" after 5 minutes of missed heartbeats; agent can re-activate by registering again

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide agent registration API endpoint (`POST /api/v1/agents`) accepting name, version, capabilities, message types accepted/emitted, authenticated via Managed Identity
- **FR-002**: System MUST provide agent discovery API endpoint (`GET /api/v1/agents?capability={name}`) returning all active agents matching the capability filter
- **FR-003**: System MUST auto-provision a dedicated Service Bus inbox queue (`agent-{id}-inbox`) for each registered agent at registration time
- **FR-004**: System MUST provide direct messaging API endpoint (`POST /api/v1/messages/send`) that validates sender identity, looks up target agent queue, and enqueues message with full envelope
- **FR-005**: System MUST provide message receive API endpoint (`GET /api/v1/messages/receive`) with long-polling support (30s timeout) returning oldest unprocessed message
- **FR-006**: System MUST provide message acknowledgment API endpoint (`POST /api/v1/messages/{messageId}/ack`) to complete message processing and remove from queue
- **FR-007**: System MUST provide event publishing API endpoint (`POST /api/v1/events/publish`) that publishes to Service Bus topic with naming convention `events.{domain}.{event-type}`
- **FR-008**: System MUST provide event subscription API endpoint (`POST /api/v1/events/subscribe`) that creates a Service Bus subscription for the calling agent with optional SQL filter rules
- **FR-009**: System MUST provide event receive API endpoint (`GET /api/v1/events/receive/{subscriptionId}`) with long-polling returning events from agent's subscription
- **FR-010**: System MUST validate all API requests using Entra ID JWT tokens with `roles` claim containing "AgentBus.Agent" or "AgentBus.Admin"
- **FR-011**: System MUST emit OpenTelemetry traces for all API requests with `x-trace-id` header propagation to Service Bus and Cosmos DB operations
- **FR-012**: System MUST log all operations using structured JSON logging via Serilog with correlation IDs to Application Insights
- **FR-013**: System MUST provide health check endpoints (`/api/v1/health` and `/api/v1/health/ready`) verifying Cosmos DB, Service Bus, and Application Insights connectivity
- **FR-014**: System MUST implement heartbeat tracking via `PATCH /api/v1/agents/{id}/heartbeat` updating `lastHeartbeat` timestamp in Cosmos DB
- **FR-015**: System MUST mark agents as "inactive" when last heartbeat exceeds 5 minutes and exclude from discovery results
- **FR-016**: System MUST persist agent registrations in Cosmos DB serverless container with partition key `/partitionKey` for optimal query performance
- **FR-017**: System MUST support deregistration via `DELETE /api/v1/agents/{id}` removing agent from registry and deleting inbox queue
- **FR-018**: System MUST version all API endpoints with `/api/v1/` prefix for future breaking changes
- **FR-019**: System MUST limit message payload size to Service Bus Standard tier limits (256 KB per message)
- **FR-020**: System MUST configure Service Bus dead-letter queues for all inbox queues and subscriptions with max delivery count of 10
- **FR-021**: System MUST emit custom metrics to Application Insights: agents_registered, messages_per_second, events_per_second, dlq_depth
- **FR-022**: System MUST configure Application Insights alerts for DLQ depth > 0 and missed heartbeats
- **FR-023**: Infrastructure MUST be provisioned using Bicep templates with parameters for environment (dev, prod)
- **FR-024**: Infrastructure MUST configure zero-secrets architecture using User-Assigned Managed Identities for Broker API and all agents
- **FR-025**: Infrastructure MUST use serverless-first resources: Container Apps (Consumption), Cosmos DB (Serverless), Service Bus (Standard)

### Key Entities

- **Agent**: Represents an autonomous agent registered in the system. Attributes: id, name, version, status (active/inactive), capabilities (array), messageTypes (accepts/emits), identity (Managed Identity IDs), endpoints (inbox queue name, health check URL), metadata (owner, environment, tags), timestamps (registeredAt, lastHeartbeat)

- **Message Envelope**: Wraps direct point-to-point messages. Attributes: messageId, correlationId, conversationId, from (sender agent ID), to (receiver agent ID), messageType, timestamp, ttl, payload (JSON), replyTo (queue name), headers (x-trace-id, x-agent-version)

- **Event Envelope**: Wraps published domain events. Attributes: eventId, eventType, source (publisher agent ID), timestamp, dataVersion, data (event payload JSON), headers (x-trace-id, x-correlation-id)

- **Capability**: Describes an agent's functional capability. Attributes: name, description, inputSchema (ref to request schema), outputSchema (ref to response schema)

- **Subscription**: Represents an agent's subscription to an event type. Attributes: subscriptionId, agentId, eventType (topic name), serviceBusSubscriptionName, filters (SQL rules), createdAt

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Two agents can complete full registration, discovery, message exchange, and message acknowledgment cycle in under 5 seconds end-to-end (P50 latency)
- **SC-002**: System supports minimum 10 concurrent agent registrations without degradation (MVP scale target)
- **SC-003**: Infrastructure deployment from clean subscription to healthy broker API completes in under 15 minutes using Bicep automation
- **SC-004**: 100% of API requests generate OpenTelemetry traces visible in Application Insights within 30 seconds with correct correlation IDs
- **SC-005**: 95% of messages delivered within 1 second from send to receive (P95 message latency)
- **SC-006**: Zero secrets or API keys stored in code, configuration, or environment variables (verified by security scan)
- **SC-007**: Health check endpoints return 200 OK when all dependencies (Cosmos DB, Service Bus, App Insights) are healthy
- **SC-008**: System handles invalid JWT tokens with 401 Unauthorized before reaching application code (verified by security test)
- **SC-009**: Dead-letter queue monitoring alerts trigger within 1 minute when failed messages appear in DLQ
- **SC-010**: Agent status transitions from "active" to "inactive" within 5 minutes of last heartbeat (automated background job)
- **SC-011**: All Gherkin BDD scenarios pass with 100% success rate in automated test suite
- **SC-012**: Infrastructure cost remains under $50/month at low traffic volumes (serverless optimization validated)

## Assumptions *(include if applicable)*

- **Azure Subscription**: Deployment assumes user has an active Azure subscription with Owner or Contributor role for resource provisioning
- **Entra ID Permissions**: User can create App Registrations and assign App Roles to Managed Identity service principals
- **Network Connectivity**: Agents can reach public endpoints; private endpoints and VNET integration are deferred to enterprise upgrade
- **.NET 9 Runtime**: Development and deployment assume .NET 9 SDK availability (LTS version)
- **Service Bus Limits**: Standard tier limits (256 KB message size, 10 GB queue size) are sufficient for MVP workloads
- **Cosmos DB Throttling**: Serverless tier with 5000 RU/s burst is sufficient for 10 concurrent agents
- **Single Region**: MVP deploys to single Azure region; multi-region replication is enterprise upgrade
- **No Ambient Authentication**: All agents MUST explicitly authenticate with Managed Identity; no API key fallback
- **Synchronous Discovery**: Agent discovery queries Cosmos DB directly; change feed streaming is enterprise upgrade
- **Auto-Topic Provisioning**: Topics are created on-demand by Broker API; pre-provisioning is not required
- **English Language**: API documentation, error messages, and logs are in English only for MVP

## Out of Scope *(include if beneficial)*

- **Agent Client SDK**: The AgentBus.Sdk NuGet package for agents to easily consume the API—deferred to post-MVP
- **Admin Dashboard**: React-based web UI for monitoring agents and messages—optional, not required for MVP
- **gRPC Support**: High-performance binary protocol for inter-agent communication—enterprise upgrade
- **WebSocket Support**: Real-time event streaming over persistent connections—enterprise upgrade
- **Multi-Region Deployment**: Active-passive or active-active replication across Azure regions—enterprise upgrade
- **Private Endpoints**: VNET integration and private connectivity for Service Bus and Cosmos DB—enterprise upgrade
- **Service Bus Premium**: Message sessions, partitioned queues, VNET isolation—enterprise upgrade
- **Cosmos DB Provisioned Throughput**: Reserved capacity for predictable performance—enterprise upgrade
- **Rate Limiting**: Per-agent throttling and quotas via Azure API Management—enterprise upgrade
- **Custom Metrics Dashboards**: Azure Monitor Workbooks and Grafana integration—enterprise upgrade
- **Agent Versioning**: Side-by-side registration of multiple agent versions with traffic splitting—enterprise upgrade
- **Message Routing Chains**: Auto-forwarding and complex routing rules—enterprise upgrade
- **Event Schema Registry**: Centralized schema validation and evolution—enterprise upgrade
- **Conditional Access Policies**: Entra ID conditional access based on device/location/risk—enterprise upgrade
- **Fine-Grained RBAC**: Per-topic publish/subscribe permissions beyond basic AgentBus.Agent role—enterprise upgrade
