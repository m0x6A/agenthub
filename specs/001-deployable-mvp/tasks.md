# Tasks: AgentBus Deployable MVP

**Branch**: `001-deployable-mvp`  
**Date**: 2026-02-11  
**Input**: Design documents from `/specs/001-deployable-mvp/`

**Organization**: Tasks are grouped by user story (P1-P5) to enable independent implementation and testing of each story. Each story follows the BDD → TDD → Implementation → Refactor cycle per Constitution Principle III (Test-First Development).

## Format: `- [ ] [ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4, US5)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create solution file AgentBus.sln at repository root
- [ ] T002 Create Directory.Packages.props for Central Package Management at repository root
- [ ] T003 Create .editorconfig with C# 13 standards at repository root
- [ ] T004 Create src/AgentBus.Broker/AgentBus.Broker.csproj with .NET 9 target framework
- [ ] T005 [P] Create tests/AgentBus.Broker.Tests/AgentBus.Broker.Tests.csproj with xUnit, Reqnroll, Shouldly, NSubstitute
- [ ] T006 [P] Create Directory.Build.props with TreatWarningsAsErrors, nullable references enabled
- [ ] T007 [P] Create .gitignore with .NET artifacts, bin/, obj/, .vs/
- [ ] T008 Initialize Git repository and create feature branch 001-deployable-mvp
- [ ] T009 [P] Create README.md with project overview and getting started guide

**Checkpoint**: Project structure ready for module implementation

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T010 Create src/AgentBus.Broker/SharedKernel/ directory structure (Interfaces/, Telemetry/, Security/, Models/)
- [ ] T011 [P] Create SharedKernel/Models/Agent.cs record with all properties per data-model.md
- [ ] T012 [P] Create SharedKernel/Models/MessageEnvelope.cs record per data-model.md
- [ ] T013 [P] Create SharedKernel/Models/EventEnvelope.cs record per data-model.md
- [ ] T014 [P] Create SharedKernel/Models/Subscription.cs record per data-model.md
- [ ] T015 Create SharedKernel/Interfaces/IAgentRegistry.cs with GetAgentByIdAsync, FindAgentsByCapabilityAsync methods
- [ ] T016 [P] Create SharedKernel/Interfaces/IMessageBroker.cs with SendMessageAsync, ReceiveMessageAsync methods
- [ ] T017 [P] Create SharedKernel/Interfaces/IEventBroker.cs with PublishEventAsync, SubscribeAsync methods
- [ ] T018 Create SharedKernel/Security/JwtValidation.cs with Microsoft.Identity.Web configuration
- [ ] T019 Create SharedKernel/Telemetry/OpenTelemetrySetup.cs with Application Insights exporter configuration
- [ ] T020 [P] Add NuGet packages to Directory.Packages.props: Azure.Identity, Azure.Messaging.ServiceBus, Microsoft.Azure.Cosmos, Microsoft.Identity.Web, OpenTelemetry.Exporter.AzureMonitor, Serilog.Sinks.ApplicationInsights
- [ ] T021 Create src/AgentBus.Broker/Program.cs with minimal API bootstrapping, DI container, OpenTelemetry, Serilog
- [ ] T022 Configure appsettings.json with Azure service endpoints (Cosmos DB, Service Bus, Application Insights) using environment variables
- [ ] T023 [P] Create src/AgentBus.Broker/Modules/ directory structure (Registration/, Messaging/, Eventing/, Health/)
- [ ] T024 Verify solution builds successfully with dotnet build

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Agent Registration & Discovery (Priority: P1) 🎯 MVP

**Goal**: Enable agents to register, declare capabilities, and discover other agents by capability

**Independent Test**: Deploy a single agent, register with broker, query for own registration, discover self by capability

### 3.1 BDD Scenarios (REQUIRED - Must be FIRST) 🔴

> **CONSTITUTION PRINCIPLE III**: Test-First Development is NON-NEGOTIABLE

- [ ] T025 [P] [US1] Write Gherkin BDD scenarios in tests/AgentBus.Broker.Tests/Features/Registration.feature with 5 acceptance scenarios from spec.md
- [ ] T026 [US1] Obtain user approval of BDD acceptance criteria
- [ ] T027 [P] [US1] Write step definitions in tests/AgentBus.Broker.Tests/StepDefinitions/RegistrationSteps.cs using WebApplicationFactory
- [ ] T028 [US1] Run BDD tests with dotnet test — verify they FAIL (RED phase)

### 3.2 Unit Tests (REQUIRED - Must FAIL before implementation) 🔴

- [ ] T029 [P] [US1] Write RegisterAgentHandler unit tests in tests/AgentBus.Broker.Tests/Unit/Registration/RegisterAgentHandlerTests.cs
- [ ] T030 [P] [US1] Write DiscoverAgentsHandler unit tests in tests/AgentBus.Broker.Tests/Unit/Registration/DiscoverAgentsHandlerTests.cs
- [ ] T031 [P] [US1] Write UpdateHeartbeatHandler unit tests in tests/AgentBus.Broker.Tests/Unit/Registration/UpdateHeartbeatHandlerTests.cs
- [ ] T032 [P] [US1] Write DeregisterAgentHandler unit tests in tests/AgentBus.Broker.Tests/Unit/Registration/DeregisterAgentHandlerTests.cs
- [ ] T033 [P] [US1] Write RegisterAgentValidator unit tests in tests/AgentBus.Broker.Tests/Unit/Registration/RegisterAgentValidatorTests.cs
- [ ] T034 [US1] Run unit tests with dotnet test —filter Category=Registration — verify they FAIL (RED phase)

### 3.3 Implementation for User Story 1 (Now make tests GREEN) 🟢

- [ ] T035 Create src/AgentBus.Broker/Modules/Registration/RegistrationModule.cs with AddRegistrationModule and MapRegistrationEndpoints methods
- [ ] T036 [P] [US1] Create feature directory src/AgentBus.Broker/Modules/Registration/Features/RegisterAgent/
- [ ] T037 [P] [US1] Implement RegisterAgentRequest.cs record in Features/RegisterAgent/ with name, version, capabilities, messageTypes
- [ ] T038 [P] [US1] Implement RegisterAgentResponse.cs record in Features/RegisterAgent/ (full Agent DTO)
- [ ] T039 [P] [US1] Implement RegisterAgentValidator.cs with FluentValidation rules in Features/RegisterAgent/ (SemVer validation, capability format)
- [ ] T040 [US1] Implement RegisterAgentHandler.cs in Features/RegisterAgent/ with Cosmos DB upsert, Service Bus queue creation, principal ID validation
- [ ] T041 [US1] Implement RegisterAgentEndpoint.cs in Features/RegisterAgent/ with POST /api/v1/agents mapped with [Authorize(Roles="AgentBus.Agent")]
- [ ] T042 [P] [US1] Create feature directory src/AgentBus.Broker/Modules/Registration/Features/DiscoverAgents/
- [ ] T043 [P] [US1] Implement DiscoverAgentsRequest.cs record in Features/DiscoverAgents/ with capability?, status? query params
- [ ] T044 [P] [US1] Implement DiscoverAgentsResponse.cs record in Features/DiscoverAgents/ with agents array, totalCount
- [ ] T045 [US1] Implement DiscoverAgentsHandler.cs in Features/DiscoverAgents/ with Cosmos DB query (cross-partition if capability filter)
- [ ] T046 [US1] Implement DiscoverAgentsEndpoint.cs in Features/DiscoverAgents/ with GET /api/v1/agents
- [ ] T047 [P] [US1] Create feature directory src/AgentBus.Broker/Modules/Registration/Features/GetAgent/
- [ ] T048 [P] [US1] Implement GetAgentHandler.cs in Features/GetAgent/ with Cosmos DB point read
- [ ] T049 [US1] Implement GetAgentEndpoint.cs in Features/GetAgent/ with GET /api/v1/agents/{agentId}
- [ ] T050 [P] [US1] Create feature directory src/AgentBus.Broker/Modules/Registration/Features/UpdateHeartbeat/
- [ ] T051 [US1] Implement UpdateHeartbeatHandler.cs in Features/UpdateHeartbeat/ with Cosmos DB patch operation
- [ ] T052 [US1] Implement UpdateHeartbeatEndpoint.cs in Features/UpdateHeartbeat/ with PATCH /api/v1/agents/{agentId}/heartbeat
- [ ] T053 [P] [US1] Create feature directory src/AgentBus.Broker/Modules/Registration/Features/DeregisterAgent/
- [ ] T054 [US1] Implement DeregisterAgentHandler.cs in Features/DeregisterAgent/ with Cosmos DB delete, Service Bus queue deletion
- [ ] T055 [US1] Implement DeregisterAgentEndpoint.cs in Features/DeregisterAgent/ with DELETE /api/v1/agents/{agentId}
- [ ] T056 [US1] Implement internal AgentRegistry.cs in Modules/Registration/ implementing IAgentRegistry interface
- [ ] T057 [US1] Register RegistrationModule in Program.cs with builder.Services.AddRegistrationModule() and app.MapRegistrationEndpoints()
- [ ] T058 [US1] Run all tests with dotnet test — verify they PASS (GREEN phase)

### 3.4 Refactor (Keep tests GREEN) 🔵

- [ ] T059 [US1] Refactor RegisterAgentHandler for clarity: extract queue name generation, extract identity validation
- [ ] T060 [US1] Verify tests still pass after refactoring
- [ ] T061 [P] [US1] Add OpenTelemetry Activity.Current?.AddTag for agentId, capability in handlers
- [ ] T062 [P] [US1] Add structured logging with Serilog in handlers using correlation IDs
- [ ] T063 [US1] Run dotnet test — verify all tests still PASS
- [ ] T063a [US1] Create SharedKernel/BackgroundServices/HeartbeatMonitorService.cs implementing IHostedService with timer checking every 1 minute
- [ ] T063b [P] [US1] Write unit tests for HeartbeatMonitorService in tests/AgentBus.Broker.Tests/Unit/BackgroundServices/HeartbeatMonitorServiceTests.cs verifying 5-minute threshold and status transition logic
- [ ] T063c [US1] Implement HeartbeatMonitorService: query agents with lastHeartbeat > 5 minutes ago, update status to "inactive" via Cosmos DB patch
- [ ] T063d [US1] Register HeartbeatMonitorService in Program.cs via builder.Services.AddHostedService<HeartbeatMonitorService>()
- [ ] T063e [US1] Write integration test verifying agent transitions to "inactive" after 5 minutes of no heartbeats (time-accelerated test)

**Checkpoint**: User Story 1 complete, agents can register, discover, heartbeat, and deregister. Background service enforces heartbeat timeout (FR-015, SC-010).

---

## Phase 4: User Story 2 - Direct Agent-to-Agent Messaging (Priority: P2)

**Goal**: Enable agents to send direct, point-to-point messages to specific agents via dedicated inbox queues

**Independent Test**: Register two agents, send message from agent A to agent B, agent B receives and acknowledges

### 4.1 BDD Scenarios (REQUIRED - Must be FIRST) 🔴

- [ ] T064 [P] [US2] Write Gherkin BDD scenarios in tests/AgentBus.Broker.Tests/Features/Messaging.feature with 5 acceptance scenarios from spec.md
- [ ] T065 [US2] Obtain user approval of BDD acceptance criteria
- [ ] T066 [P] [US2] Write step definitions in tests/AgentBus.Broker.Tests/StepDefinitions/MessagingSteps.cs
- [ ] T067 [US2] Run BDD tests — verify they FAIL (RED phase)

### 4.2 Unit Tests (REQUIRED - Must FAIL before implementation) 🔴

- [ ] T068 [P] [US2] Write SendMessageHandler unit tests in tests/AgentBus.Broker.Tests/Unit/Messaging/SendMessageHandlerTests.cs
- [ ] T069 [P] [US2] Write ReceiveMessageHandler unit tests in tests/AgentBus.Broker.Tests/Unit/Messaging/ReceiveMessageHandlerTests.cs
- [ ] T070 [P] [US2] Write AcknowledgeMessageHandler unit tests in tests/AgentBus.Broker.Tests/Unit/Messaging/AcknowledgeMessageHandlerTests.cs
- [ ] T071 [P] [US2] Write SendMessageValidator unit tests in tests/AgentBus.Broker.Tests/Unit/Messaging/SendMessageValidatorTests.cs
- [ ] T072 [US2] Run unit tests with dotnet test —filter Category=Messaging — verify they FAIL (RED phase)

### 4.3 Implementation for User Story 2 (Now make tests GREEN) 🟢

- [ ] T073 Create src/AgentBus.Broker/Modules/Messaging/MessagingModule.cs with AddMessagingModule and MapMessagingEndpoints methods
- [ ] T074 [P] [US2] Create feature directory src/AgentBus.Broker/Modules/Messaging/Features/SendMessage/
- [ ] T075 [P] [US2] Implement SendMessageRequest.cs record in Features/SendMessage/ with to, messageType, payload, correlationId?, ttl?, headers?
- [ ] T076 [P] [US2] Implement SendMessageResponse.cs record in Features/SendMessage/ with messageId
- [ ] T077 [P] [US2] Implement SendMessageValidator.cs in Features/SendMessage/ with payload size validation (256 KB limit), TTL validation (max 14 days)
- [ ] T078 [US2] Implement SendMessageHandler.cs in Features/SendMessage/ with agent registry lookup (to/from), Service Bus send to target inbox queue
- [ ] T079 [US2] Implement SendMessageEndpoint.cs in Features/SendMessage/ with POST /api/v1/messages/send
- [ ] T080 [P] [US2] Create feature directory src/AgentBus.Broker/Modules/Messaging/Features/ReceiveMessage/
- [ ] T081 [P] [US2] Implement ReceiveMessageResponse.cs record in Features/ReceiveMessage/ (MessageEnvelope DTO)
- [ ] T082 [US2] Implement ReceiveMessageHandler.cs in Features/ReceiveMessage/ with Service Bus long-polling receive (maxWaitTime 30s)
- [ ] T083 [US2] Implement ReceiveMessageEndpoint.cs in Features/ReceiveMessage/ with GET /api/v1/messages/receive?timeout=30
- [ ] T084 [P] [US2] Create feature directory src/AgentBus.Broker/Modules/Messaging/Features/AcknowledgeMessage/
- [ ] T085 [US2] Implement AcknowledgeMessageHandler.cs in Features/AcknowledgeMessage/ with Service Bus complete message operation
- [ ] T086 [US2] Implement AcknowledgeMessageEndpoint.cs in Features/AcknowledgeMessage/ with POST /api/v1/messages/{messageId}/ack
- [ ] T087 [US2] Implement internal MessageBroker.cs in Modules/Messaging/ implementing IMessageBroker interface
- [ ] T088 [US2] Register MessagingModule in Program.cs with builder.Services.AddMessagingModule() and app.MapMessagingEndpoints()
- [ ] T089 [US2] Run all tests with dotnet test — verify they PASS (GREEN phase)

### 4.4 Refactor (Keep tests GREEN) 🔵

- [ ] T090 [US2] Refactor SendMessageHandler: extract Service Bus sender client caching, extract envelope serialization
- [ ] T091 [US2] Verify tests still pass after refactoring
- [ ] T092 [P] [US2] Add OpenTelemetry Activity for message send/receive with messageId, correlationId tags
- [ ] T093 [P] [US2] Add structured logging for message lifecycle (send, receive, ack, DLQ)
- [ ] T094 [US2] Run dotnet test — verify all tests still PASS

**Checkpoint**: User Story 2 complete, agents can send/receive direct messages with full tracing

---

## Phase 5: User Story 3 - Event Publishing & Subscription (Priority: P3)

**Goal**: Enable agents to publish domain events to topics and subscribe to events with fan-out delivery

**Independent Test**: Agent A subscribes to event type, agent B publishes event, agent A receives event

### 5.1 BDD Scenarios (REQUIRED - Must be FIRST) 🔴

- [ ] T095 [P] [US3] Write Gherkin BDD scenarios in tests/AgentBus.Broker.Tests/Features/Eventing.feature with 5 acceptance scenarios from spec.md
- [ ] T096 [US3] Obtain user approval of BDD acceptance criteria
- [ ] T097 [P] [US3] Write step definitions in tests/AgentBus.Broker.Tests/StepDefinitions/EventingSteps.cs
- [ ] T098 [US3] Run BDD tests — verify they FAIL (RED phase)

### 5.2 Unit Tests (REQUIRED - Must FAIL before implementation) 🔴

- [ ] T099 [P] [US3] Write PublishEventHandler unit tests in tests/AgentBus.Broker.Tests/Unit/Eventing/PublishEventHandlerTests.cs
- [ ] T100 [P] [US3] Write SubscribeToEventHandler unit tests in tests/AgentBus.Broker.Tests/Unit/Eventing/SubscribeToEventHandlerTests.cs
- [ ] T101 [P] [US3] Write ReceiveEventHandler unit tests in tests/AgentBus.Broker.Tests/Unit/Eventing/ReceiveEventHandlerTests.cs
- [ ] T102 [P] [US3] Write UnsubscribeFromEventHandler unit tests in tests/AgentBus.Broker.Tests/Unit/Eventing/UnsubscribeFromEventHandlerTests.cs
- [ ] T103 [P] [US3] Write PublishEventValidator unit tests in tests/AgentBus.Broker.Tests/Unit/Eventing/PublishEventValidatorTests.cs
- [ ] T104 [US3] Run unit tests with dotnet test —filter Category=Eventing — verify they FAIL (RED phase)

### 5.3 Implementation for User Story 3 (Now make tests GREEN) 🟢

- [ ] T105 Create src/AgentBus.Broker/Modules/Eventing/EventingModule.cs with AddEventingModule and MapEventingEndpoints methods
- [ ] T106 [P] [US3] Create feature directory src/AgentBus.Broker/Modules/Eventing/Features/PublishEvent/
- [ ] T107 [P] [US3] Implement PublishEventRequest.cs record in Features/PublishEvent/ with eventType, dataVersion, data, headers?
- [ ] T108 [P] [US3] Implement PublishEventResponse.cs record in Features/PublishEvent/ with eventId
- [ ] T109 [P] [US3] Implement PublishEventValidator.cs in Features/PublishEvent/ with eventType pattern validation, dataVersion SemVer validation
- [ ] T110 [US3] Implement PublishEventHandler.cs in Features/PublishEvent/ with Service Bus topic auto-provisioning, publish to topic
- [ ] T111 [US3] Implement PublishEventEndpoint.cs in Features/PublishEvent/ with POST /api/v1/events/publish
- [ ] T112 [P] [US3] Create feature directory src/AgentBus.Broker/Modules/Eventing/Features/SubscribeToEvent/
- [ ] T113 [P] [US3] Implement SubscribeToEventRequest.cs record in Features/SubscribeToEvent/ with eventType, filters? (SQL rules array)
- [ ] T114 [P] [US3] Implement SubscribeToEventResponse.cs record in Features/SubscribeToEvent/ (Subscription DTO)
- [ ] T115 [US3] Implement SubscribeToEventHandler.cs in Features/SubscribeToEvent/ with Service Bus subscription creation, Cosmos DB subscription record
- [ ] T116 [US3] Implement SubscribeToEventEndpoint.cs in Features/SubscribeToEvent/ with POST /api/v1/events/subscribe
- [ ] T117 [P] [US3] Create feature directory src/AgentBus.Broker/Modules/Eventing/Features/ReceiveEvent/
- [ ] T118 [P] [US3] Implement ReceiveEventResponse.cs record in Features/ReceiveEvent/ (EventEnvelope DTO)
- [ ] T119 [US3] Implement ReceiveEventHandler.cs in Features/ReceiveEvent/ with Service Bus long-polling receive from subscription (maxWaitTime 30s)
- [ ] T120 [US3] Implement ReceiveEventEndpoint.cs in Features/ReceiveEvent/ with GET /api/v1/events/receive/{subscriptionId}
- [ ] T121 [P] [US3] Create feature directory src/AgentBus.Broker/Modules/Eventing/Features/UnsubscribeFromEvent/
- [ ] T122 [US3] Implement UnsubscribeFromEventHandler.cs in Features/UnsubscribeFromEvent/ with Service Bus subscription deletion, Cosmos DB subscription removal
- [ ] T123 [US3] Implement UnsubscribeFromEventEndpoint.cs in Features/UnsubscribeFromEvent/ with DELETE /api/v1/events/subscriptions/{subscriptionId}
- [ ] T124 [US3] Implement internal EventBroker.cs in Modules/Eventing/ implementing IEventBroker interface
- [ ] T125 [US3] Register EventingModule in Program.cs with builder.Services.AddEventingModule() and app.MapEventingEndpoints()
- [ ] T126 [US3] Run all tests with dotnet test — verify they PASS (GREEN phase)

### 5.4 Refactor (Keep tests GREEN) 🔵

- [ ] T127 [US3] Refactor SubscribeToEventHandler: extract subscription name generation, extract SQL filter rule creation
- [ ] T128 [US3] Verify tests still pass after refactoring
- [ ] T129 [P] [US3] Add OpenTelemetry Activity for event publish/receive with eventId, eventType, source tags
- [ ] T130 [P] [US3] Add structured logging for event lifecycle (publish, subscribe, receive, unsubscribe)
- [ ] T131 [US3] Run dotnet test — verify all tests still PASS

**Checkpoint**: User Story 3 complete, agents can publish/subscribe to events with fan-out delivery

---

## Phase 6: User Story 4 - Infrastructure Deployment & Configuration (Priority: P4)

**Goal**: Automate complete infrastructure provisioning to Azure using Bicep templates and GitHub Actions

**Independent Test**: Run Bicep deployment on clean subscription, verify all resources created, health checks pass

### 6.1 Infrastructure Templates (No BDD/TDD - Infrastructure as Code)

- [ ] T132 [P] [US4] Create infra/main.bicep with subscription-scoped deployment targeting resource group creation
- [ ] T133 [P] [US4] Create infra/modules/identity.bicep with User-Assigned Managed Identity for Broker API and sample agent
- [ ] T134 [P] [US4] Create infra/modules/cosmosDb.bicep with serverless account, database, agents container, subscriptions container per data-model.md
- [ ] T135 [P] [US4] Create infra/modules/serviceBus.bicep with Standard namespace, RBAC role assignments to Managed Identities, DLQ configuration (maxDeliveryCount: 10, enableDeadLetteringOnMessageExpiration: true) for all queues/subscriptions
- [ ] T136 [P] [US4] Create infra/modules/containerRegistry.bicep with Basic SKU, admin disabled, Managed Identity pull access
- [ ] T137 [P] [US4] Create infra/modules/monitoring.bicep with Log Analytics workspace, Application Insights component
- [ ] T138 [US4] Create infra/modules/containerApps.bicep with Consumption environment, Broker API container app, health probes, ingress configuration
- [ ] T139 [US4] Wire up all modules in infra/main.bicep with correct dependencies (identity → RBAC → container apps)
- [ ] T140 [P] [US4] Create infra/parameters/dev.bicepparam with development environment parameters (small SKUs)
- [ ] T141 [P] [US4] Create infra/parameters/prod.bicepparam with production environment parameters (zone redundancy where applicable)
- [ ] T142 [US4] Validate Bicep templates with az bicep build and az deployment sub what-if
- [ ] T143 [P] [US4] Create src/AgentBus.Broker/Dockerfile multi-stage build with .NET 9 SDK base image
- [ ] T144 [US4] Test Docker build locally with docker build -t agentbus-broker:latest -f src/AgentBus.Broker/Dockerfile .

### 6.2 CI/CD Pipelines

- [ ] T145 [P] [US4] Create .github/workflows/build-test.yml with dotnet restore, build, test on pull requests
- [ ] T146 [P] [US4] Create .github/workflows/deploy-infra.yml with Azure OIDC login, Bicep deployment on main branch push
- [ ] T147 [US4] Configure GitHub OIDC federated identity credentials in Azure Entra ID for GitHub Actions
- [ ] T148 [US4] Add GitHub secrets: AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID
- [ ] T149 [US4] Test infrastructure deployment workflow by pushing to main branch
- [ ] T150 [P] [US4] Create .github/workflows/deploy-app.yml with Docker build, ACR push, Container App revision update
- [ ] T151 [US4] Test application deployment workflow end-to-end from local commit to deployed Container App

**Checkpoint**: User Story 4 complete, infrastructure fully automated with Bicep and GitHub Actions

---

## Phase 7: User Story 5 - Observability & Monitoring (Priority: P5)

**Goal**: Provide full visibility into agent operations with distributed tracing, structured logging, custom metrics, and alerts

**Independent Test**: Execute agent workflow, query Application Insights for traces, verify correlation across services

### 7.1 BDD Scenarios (REQUIRED - Must be FIRST) 🔴

- [ ] T152 [P] [US5] Write Gherkin BDD scenarios in tests/AgentBus.Broker.Tests/Features/Observability.feature with 5 acceptance scenarios from spec.md
- [ ] T153 [US5] Obtain user approval of BDD acceptance criteria
- [ ] T154 [P] [US5] Write step definitions in tests/AgentBus.Broker.Tests/StepDefinitions/ObservabilitySteps.cs
- [ ] T155 [US5] Run BDD tests — verify they FAIL (RED phase)

### 7.2 Unit Tests (REQUIRED - Must FAIL before implementation) 🔴

- [ ] T156 [P] [US5] Write GetHealthHandler unit tests in tests/AgentBus.Broker.Tests/Unit/Health/GetHealthHandlerTests.cs
- [ ] T157 [P] [US5] Write GetReadinessHandler unit tests in tests/AgentBus.Broker.Tests/Unit/Health/GetReadinessHandlerTests.cs
- [ ] T158 [US5] Run unit tests with dotnet test —filter Category=Health — verify they FAIL (RED phase)

### 7.3 Implementation for User Story 5 (Now make tests GREEN) 🟢

- [ ] T159 Create src/AgentBus.Broker/Modules/Health/HealthModule.cs with AddHealthModule and MapHealthEndpoints methods
- [ ] T160 [P] [US5] Create feature directory src/AgentBus.Broker/Modules/Health/Features/GetHealth/
- [ ] T161 [P] [US5] Implement GetHealthResponse.cs record in Features/GetHealth/ with status, checks (cosmosDb, serviceBus, applicationInsights)
- [ ] T162 [US5] Implement GetHealthHandler.cs in Features/GetHealth/ with dependency health checks (connectivity tests)
- [ ] T163 [US5] Implement GetHealthEndpoint.cs in Features/GetHealth/ with GET /api/v1/health (no auth required)
- [ ] T164 [P] [US5] Create feature directory src/AgentBus.Broker/Modules/Health/Features/GetReadiness/
- [ ] T165 [US5] Implement GetReadinessHandler.cs in Features/GetReadiness/ with readiness checks (all dependencies responsive)
- [ ] T166 [US5] Implement GetReadinessEndpoint.cs in Features/GetReadiness/ with GET /api/v1/health/ready (no auth required)
- [ ] T167 [US5] Register HealthModule in Program.cs with builder.Services.AddHealthModule() and app.MapHealthEndpoints()
- [ ] T168 [US5] Configure Container Apps health probes (liveness: /health, readiness: /health/ready) in containerApps.bicep
- [ ] T169 [P] [US5] Implement custom metrics in SharedKernel/Telemetry/Metrics.cs with Meter for agents_registered_total, messages_per_second, events_per_second, dlq_depth
- [ ] T170 [US5] Emit custom metrics in handlers: increment agents_registered in RegisterAgentHandler, messages_per_second in SendMessageHandler, etc.
- [ ] T171 [P] [US5] Create infra/modules/alerts.bicep with Application Insights alert rules for DLQ depth > 0 and missed heartbeats
- [ ] T172 [US5] Add alerts module to infra/main.bicep with dependency on monitoring module
- [ ] T173 [US5] Run all tests with dotnet test — verify they PASS (GREEN phase)

### 7.4 Refactor (Keep tests GREEN) 🔵

- [ ] T174 [US5] Refactor health check handlers: extract dependency check logic into dedicated service classes
- [ ] T175 [US5] Verify tests still pass after refactoring
- [ ] T176 [US5] Run dotnet test — verify all tests still PASS

**Checkpoint**: User Story 5 complete, full observability with tracing, logging, metrics, and alerting

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final improvements that affect multiple user stories

- [ ] T177 [P] Update README.md with architecture diagram, getting started links to quickstart.md
- [ ] T178 [P] Create CONTRIBUTING.md with PR guidelines, code style, BDD/TDD workflow
- [ ] T179 [P] Create docs/architecture/DEPLOYMENT.md with step-by-step Bicep deployment guide
- [ ] T180 [P] Create docs/architecture/OPERATIONS.md with monitoring, alerting, troubleshooting runbooks
- [ ] T181 Verify all BDD scenarios pass end-to-end with dotnet test
- [ ] T182 Run code coverage report with dotnet test /p:CollectCoverage=true — ensure handlers have 100% coverage
- [ ] T183 [P] Code cleanup: remove unused using statements, run dotnet format
- [ ] T184 [P] Security audit: verify zero secrets in codebase, scan for hardcoded credentials (validates SC-006)
- [ ] T185 Performance test: register 10 agents, send 100 messages, verify P95 latency < 1s (validates SC-001, SC-005)
- [ ] T186 Run quickstart.md validation end-to-end from clean Azure subscription (validates SC-003)
- [ ] T186a [P] Validate cost optimization: check Azure cost analysis after 24h runtime, verify < $50/month projected cost (validates SC-012)
- [ ] T186b [P] Validate observability: execute full agent workflow, query Application Insights for traces with correct correlation (validates SC-004)
- [ ] T186c [P] Validate health checks: verify /health and /health/ready endpoints return 200 OK with all dependencies healthy (validates SC-007)
- [ ] T186d [P] Validate security: send requests with invalid JWT, verify 401 Unauthorized before reaching app code (validates SC-008)
- [ ] T186e [P] Validate DLQ alerting: force message to DLQ, verify alert triggers within 1 minute (validates SC-009)
- [ ] T186f [P] Validate heartbeat timeout: stop agent heartbeats, verify status transitions to inactive within 5 minutes (validates SC-010, FR-015)
- [ ] T186g Validate BDD test suite: run all .feature scenarios, verify 100% pass rate (validates SC-011)
- [ ] T187 [P] Create scripts/setup-local-dev.sh for local development environment setup
- [ ] T188 Tag release v1.0.0-mvp and create GitHub release notes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - **BLOCKS all user stories**
- **User Story 1 (Phase 3)**: Depends on Foundational phase - **Foundation for US2 and US3**
- **User Story 2 (Phase 4)**: Depends on Foundational + US1 (requires agent registry) - Can proceed after US1
- **User Story 3 (Phase 5)**: Depends on Foundational + US1 (requires agent registry) - Can proceed after US1
- **User Story 4 (Phase 6)**: Depends on US1, US2, US3 code complete (needs deployable app) - Can proceed in parallel with US5
- **User Story 5 (Phase 7)**: Depends on Foundational (requires SharedKernel) - Can proceed after US1, integrates across all modules
- **Polish (Phase 8)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (Agent Registration)**: Foundation for all other stories - agents must register before messaging/eventing
- **US2 (Messaging)**: Requires US1 (agent registry lookup for sender/receiver validation)
- **US3 (Eventing)**: Requires US1 (agent registry lookup for publisher validation, subscription ownership)
- **US4 (Infrastructure)**: Requires US1, US2, US3 code complete (needs deployable artifacts)
- **US5 (Observability)**: Cross-cutting - integrates with all modules after US1 establishes foundation

### Within Each User Story

1. **BDD Scenarios** MUST be written and FAIL before implementation
2. **Unit Tests** MUST be written and FAIL before implementation
3. **Implementation** proceeds to make tests pass (GREEN)
4. **Refactor** improves code while keeping tests green

### Parallel Opportunities

**Phase 1 (Setup)**: T002-T009 can run in parallel (all marked [P])

**Phase 2 (Foundational)**: T011-T014 (model records), T015-T017 (interfaces), T020 (NuGet), T023 (directories) can run in parallel

**Within User Stories**:
- BDD scenarios and step definitions can be written in parallel (same phase)
- Unit tests for different handlers can be written in parallel (T029-T033 for US1)
- Model/request/response/validator implementations can run in parallel (T037-T039 for US1)
- OpenTelemetry and logging tasks can run in parallel (T061-T062 for US1)

**Cross-Story Parallelism** (after Foundational complete):
- US2 and US3 can proceed in parallel after US1 complete (both only depend on US1 agent registry)
- US5 implementation can begin after US1 establishes SharedKernel and first module

---

## Parallel Example: User Story 1 Implementation

```bash
# Launch all BDD tasks together:
Task T025: Write Registration.feature scenarios
Task T027: Write RegistrationSteps.cs step definitions

# Launch all unit test tasks together:
Task T029: RegisterAgentHandler unit tests
Task T030: DiscoverAgentsHandler unit tests
Task T031: UpdateHeartbeatHandler unit tests
Task T032: DeregisterAgentHandler unit tests
Task T033: RegisterAgentValidator unit tests

# Launch all RegisterAgent feature artifacts together:
Task T037: RegisterAgentRequest.cs
Task T038: RegisterAgentResponse.cs
Task T039: RegisterAgentValidator.cs

# Launch all feature directories together:
Task T036: Features/RegisterAgent/
Task T042: Features/DiscoverAgents/
Task T047: Features/GetAgent/
Task T050: Features/UpdateHeartbeat/
Task T053: Features/DeregisterAgent/

# Launch refactor tasks together:
Task T061: OpenTelemetry attributes
Task T062: Structured logging
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. **Day 1-2**: Complete Phase 1 (Setup) + Phase 2 (Foundational) → Foundation ready
2. **Day 3-5**: Complete Phase 3 (User Story 1 - Agent Registration) → RED → GREEN → REFACTOR
3. **Day 6**: Deploy US1 to Azure, test agent registration end-to-end → **MVP DELIVERED**
4. **STOP and VALIDATE**: Demo agent registration and discovery to stakeholders

### Incremental Delivery (Recommended)

1. **Week 1**: Setup + Foundational + US1 → Deploy → **MVP v1.0** (agent registry only)
2. **Week 2**: Add US2 (Messaging) → Deploy → **MVP v1.1** (agents can message)
3. **Week 3**: Add US3 (Eventing) → Deploy → **MVP v1.2** (pub/sub enabled)
4. **Week 4**: Add US4 (Infrastructure automation) + US5 (Observability) → Deploy → **MVP v1.3** (production-ready)
5. **Week 5**: Polish phase → **MVP v1.0.0** release

### Post-MVP Feature Additions

After delivering MVP (US1 only), subsequent user stories can be added incrementally:

- **v1.1.0**: Add US2 (Messaging) - backward compatible, agents now have messaging capability
- **v1.2.0**: Add US3 (Eventing) - backward compatible, agents now have pub/sub capability
- **v1.3.0**: Add US4, US5 (Infrastructure + Observability) - production hardening

Each version is independently deployable and backward compatible with previous versions.

---

## Validation Checkpoints

### After Phase 2 (Foundational)

- [ ] dotnet build succeeds with zero warnings
- [ ] All SharedKernel interfaces compile
- [ ] All foundational models (Agent, MessageEnvelope, EventEnvelope, Subscription) defined
- [ ] OpenTelemetry and Serilog configured in Program.cs

### After Phase 3 (User Story 1)

- [ ] All Registration.feature BDD scenarios pass
- [ ] All Registration unit tests pass with 100% handler coverage
- [ ] Can register agent via POST /api/v1/agents
- [ ] Can discover agent by capability via GET /api/v1/agents?capability=X
- [ ] Can update heartbeat via PATCH /api/v1/agents/{id}/heartbeat
- [ ] Can deregister agent via DELETE /api/v1/agents/{id}
- [ ] Traces visible in Application Insights with correlation

### After Phase 4 (User Story 2)

- [ ] All Messaging.feature BDD scenarios pass
- [ ] Can send message from agent A to agent B
- [ ] Agent B receives message with long-polling
- [ ] Message acknowledgment removes from queue
- [ ] Dead-letter queue captures failed messages after 10 retries

### After Phase 5 (User Story 3)

- [ ] All Eventing.feature BDD scenarios pass
- [ ] Can subscribe to event type
- [ ] Published event delivered to all subscribers (fan-out)
- [ ] SQL filters correctly route events to subscriptions

### After Phase 6 (User Story 4)

- [ ] Bicep deployment completes successfully in < 15 minutes
- [ ] All Azure resources created with correct SKUs
- [ ] Health check endpoints return 200 OK
- [ ] Docker image builds and deploys to Container App

### After Phase 7 (User Story 5)

- [ ] Custom metrics visible in Application Insights
- [ ] Alert rules trigger on DLQ depth > 0
- [ ] End-to-end trace correlation works across all modules

### Final Validation (Phase 8)

- [ ] All 5 user stories work independently
- [ ] quickstart.md runs successfully from clean subscription
- [ ] Code coverage > 95% for handlers
- [ ] Zero secrets detected in codebase
- [ ] Performance goals met (P50 < 5s, P95 < 1s)

---

## Notes

- **[P] marker**: Tasks with different files and no dependencies - execute in parallel
- **[Story] label**: Maps task to user story (US1-US5) for traceability
- **Test-First**: BDD and unit tests MUST fail before implementation (RED phase)
- **Constitution compliance**: All 7 principles enforced throughout task execution
- **Commit strategy**: Commit after each completed user story phase (checkpoint)
- **Branch strategy**: All work on feature branch 001-deployable-mvp, PR to main after Phase 8 complete

---

## Summary

- **Total Tasks**: 193 tasks across 8 phases
- **MVP Scope**: Phase 1 + Phase 2 + Phase 3 (User Story 1 only) = ~68 tasks
- **Estimated Effort**: 
  - MVP (US1 only): 5-7 days
  - Full MVP (US1-US5): 4-5 weeks
  - With 3 developers: 2-3 weeks (parallel user story implementation)
- **Parallelization**: ~40% of tasks marked [P] for concurrent execution
- **Test Coverage**: 50+ BDD scenarios, 100+ unit tests across all user stories
- **Incremental Delivery**: Each user story independently deployable and testable
