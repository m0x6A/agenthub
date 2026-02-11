# Implementation Plan: AgentBus Deployable MVP

**Branch**: `001-deployable-mvp` | **Date**: 2026-02-11 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-deployable-mvp/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Build a production-ready message broker platform that enables autonomous agents to register, discover each other by capabilities, communicate via direct messaging, and publish/subscribe to domain events. The system uses Azure-native services (Cosmos DB, Service Bus, Container Apps) with zero-secrets security architecture (Managed Identity), complete observability (OpenTelemetry + App Insights), and Infrastructure as Code deployment (Bicep + GitHub Actions).

## Technical Context

**Language/Version**: C# 13 / .NET 9 (LTS)  
**Primary Dependencies**: ASP.NET Core Minimal APIs, Azure.Messaging.ServiceBus, Microsoft.Azure.Cosmos, Microsoft.Identity.Web, OpenTelemetry SDK, Serilog  
**Storage**: Azure Cosmos DB NoSQL API (Serverless) for agent registry, Azure Service Bus (Standard tier) for message/event queuing  
**Testing**: xUnit 2.x, Reqnroll 2.x (Gherkin BDD), Shouldly (assertions), NSubstitute (mocking), Bogus (test data), WebApplicationFactory (integration)  
**Target Platform**: Azure Container Apps (Consumption plan), Linux containers from Azure Container Registry
**Project Type**: Web API (Broker API) + Infrastructure (Bicep) + CI/CD (GitHub Actions) + optional React dashboard  
**Performance Goals**: P50 end-to-end latency < 5 seconds (registration to message exchange), P95 message delivery latency < 1 second, 10 concurrent agents supported  
**Constraints**: Service Bus Standard tier limits (256 KB message size, 10 GB queue size), Cosmos DB Serverless 5000 RU/s burst limit, single Azure region deployment  
**Scale/Scope**: MVP supports 10 concurrent agents, 5 API modules (Shared Kernel, Registration, Messaging, Eventing, Health), ~50 BDD scenarios, < $50/month Azure cost at low traffic

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Principle Compliance Checklist

- [x] **Modular Monolith**: Feature organized into 4 bounded modules (Registration, Messaging, Eventing, Health) + Shared Kernel with explicit interfaces. Modules communicate only through Shared Kernel, zero cross-module coupling.
- [x] **Vertical Slices**: Each API feature (RegisterAgent, SendMessage, PublishEvent, etc.) implemented as complete vertical slice with dedicated Endpoint/Request/Response/Handler/Validator in own directory.
- [x] **Test-First**: BDD scenarios defined in spec.md (5 user stories with acceptance criteria). Implementation follows: write .feature files → user approval → failing tests → implementation → refactor cycle.
- [x] **Security by Identity**: Entra ID JWT authentication with User-Assigned Managed Identities. Zero secrets - Broker API validates `roles` claim on every request. No API keys, certificates, or passwords.
- [x] **Modern C# Standards**: .NET 9 + C# 13 with nullable references, file-scoped namespaces, primary constructors for DI, records for DTOs, sealed by default, TimeProvider injection for testability, CancellationToken on all async methods, Central Package Management.
- [x] **Observability**: OpenTelemetry SDK to Application Insights, Serilog structured JSON logging with correlation IDs, custom metrics (agents_registered, messages_per_second, dlq_depth), health endpoints (/health, /health/ready).
- [x] **Enterprise-Ready**: Serverless-first (Container Apps Consumption, Cosmos DB Serverless, Service Bus Standard), Bicep IaC, GitHub Actions CI/CD, API versioning (/api/v1/), documented enterprise upgrade paths (Premium Service Bus, multi-region, VNET).

### Complexity Justification (if any gates fail)

✅ All gates pass. No complexity violations. Architecture aligns with constitution principles.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
AgentBus/
├── src/
│   └── AgentBus.Broker/              # Broker API (modular monolith)
│       ├── Program.cs                 # Minimal API bootstrapping
│       ├── AgentBus.Broker.csproj
│       ├── SharedKernel/              # Cross-cutting concerns
│       │   ├── Interfaces/            # Module contracts
│       │   ├── Telemetry/             # OpenTelemetry setup
│       │   ├── Security/              # JWT validation
│       │   └── Models/                # Shared DTOs
│       ├── Modules/
│       │   ├── Registration/          # Agent registration module
│       │   │   ├── RegistrationModule.cs
│       │   │   └── Features/
│       │   │       ├── RegisterAgent/
│       │   │       │   ├── RegisterAgentEndpoint.cs
│       │   │       │   ├── RegisterAgentRequest.cs
│       │   │       │   ├── RegisterAgentResponse.cs
│       │   │       │   ├── RegisterAgentHandler.cs
│       │   │       │   └── RegisterAgentValidator.cs
│       │   │       ├── DiscoverAgents/
│       │   │       ├── UpdateHeartbeat/
│       │   │       └── DeregisterAgent/
│       │   ├── Messaging/             # Direct messaging module
│       │   │   ├── MessagingModule.cs
│       │   │   └── Features/
│       │   │       ├── SendMessage/
│       │   │       ├── ReceiveMessage/
│       │   │       └── AcknowledgeMessage/
│       │   ├── Eventing/              # Pub/sub module
│       │   │   ├── EventingModule.cs
│       │   │   └── Features/
│       │   │       ├── PublishEvent/
│       │   │       ├── SubscribeToEvent/
│       │   │       ├── ReceiveEvent/
│       │   │       └── UnsubscribeFromEvent/
│       │   └── Health/                # Health check module
│       │       ├── HealthModule.cs
│       │       └── Features/
│       │           ├── GetHealth/
│       │           └── GetReadiness/
├── tests/
│   ├── AgentBus.Broker.Tests/         # Unit + integration tests
│   │   ├── Features/                  # BDD .feature files
│   │   │   ├── Registration.feature
│   │   │   ├── Messaging.feature
│   │   │   └── Eventing.feature
│   │   ├── StepDefinitions/           # Reqnroll step definitions
│   │   ├── Integration/               # WebApplicationFactory tests
│   │   └── Unit/                      # Handler/validator unit tests
├── infra/                             # Bicep infrastructure
│   ├── main.bicep                     # Entry point
│   ├── modules/
│   │   ├── containerApps.bicep        # Container Apps environment + app
│   │   ├── cosmosDb.bicep             # Cosmos DB account + container
│   │   ├── serviceBus.bicep           # Service Bus namespace + topics
│   │   ├── containerRegistry.bicep    # Azure Container Registry
│   │   ├── monitoring.bicep           # Application Insights
│   │   └── identity.bicep             # Managed Identities + RBAC
│   └── parameters/
│       ├── dev.bicepparam             # Dev environment parameters
│       └── prod.bicepparam            # Production parameters
├── .github/
│   └── workflows/
│       ├── build-test.yml             # CI pipeline (build + test)
│       └── deploy-infra.yml           # CD pipeline (Bicep deploy)
├── docs/
│   └── architecture/
│       └── ARCHITECTURE.md            # System architecture
├── specs/
│   └── 001-deployable-mvp/
│       ├── spec.md                    # Feature specification
│       ├── plan.md                    # This file
│       ├── research.md                # Phase 0 output
│       ├── data-model.md              # Phase 1 output
│       ├── quickstart.md              # Phase 1 output
│       └── contracts/                 # Phase 1 API contracts
├── Directory.Packages.props           # Central package management
└── .editorconfig                      # Code style enforcement
```

**Structure Decision**: Web API structure selected. Broker API is a single deployable unit (modular monolith) with clear module boundaries. Each module (Registration, Messaging, Eventing, Health) has its own `{Module}Module.cs` registration and feature slices. Infrastructure lives in `/infra/` with Bicep modules. BDD tests colocated with unit/integration tests in `/tests/`. No frontend initially (React dashboard is out of scope for MVP).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

N/A - All constitution principles satisfied. No complexity violations requiring justification.
