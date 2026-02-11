<!--
Sync Impact Report — Constitution v1.0.0 (2026-02-11)
=====================================================
Version Change: INITIAL → 1.0.0
Change Type: MAJOR (Initial constitution establishment)

Principles Established:
- I. Modular Monolith with Clear Seams
- II. Vertical Slice Architecture
- III. Test-First Development (NON-NEGOTIABLE)
- IV. Security by Identity
- V. Modern C# Standards
- VI. Observability from Day 1
- VII. Enterprise-Ready MVP

Sections Added:
- Technology Stack Standards
- Architecture Governance

Templates Requiring Updates:
- ✅ constitution-template.md (source template)
- ✅ plan-template.md (Constitution Check section aligned with 7 principles)
- ✅ spec-template.md (Requirements section references constitution principles)
- ✅ tasks-template.md (Task phases now enforce BDD→TDD→Implementation→Refactor cycle)

Follow-up TODOs:
- None (all placeholders filled)

Commit Message Suggestion:
  docs: establish AgentBus constitution v1.0.0
  
  - Define 7 core architectural principles
  - Establish technology stack standards
  - Set governance rules for module boundaries, testing, and code quality
  - Align with ARCHITECTURE.md (v0.1 MVP/PoC)
=====================================================
-->

# AgentBus Constitution

## Core Principles

### I. Modular Monolith with Clear Seams
**Single deployable unit with service boundaries.** The Broker API MUST be implemented as a modular monolith where each module (Registration, Messaging, Eventing) represents a bounded context with its own domain, features, and infrastructure. Modules MUST communicate only through well-defined interfaces in the Shared Kernel — never by reaching into each other's internals. No circular dependencies permitted — all dependencies flow inward toward Shared Kernel.

**Rationale:** Provides development simplicity of a monolith with architectural boundaries of microservices, enabling future extraction to independent services without re-architecture. The MVP's clear seams ensure every component can be swapped for enterprise-grade equivalents.

### II. Vertical Slice Architecture
**Self-contained features, no layered abstractions.** Each feature MUST be implemented as a complete vertical slice: request → validation → handler → persistence → response. Features own their entire flow end-to-end within a single directory. NO repository layers spanning features, NO service layers shared across slices, NO generic abstractions unless proven necessary by three or more concrete use cases.

**Rationale:** Reduces coupling, improves discoverability, and enables independent evolution of features. Changes to one feature do not cascade through shared abstraction layers.

### III. Test-First Development (NON-NEGOTIABLE)
**Outside-in BDD + TDD cycle strictly enforced.** For every feature:
1. Write Gherkin BDD scenario describing desired behavior (`.feature` file)
2. Obtain user approval of acceptance criteria
3. Write step definitions — tests MUST fail (RED phase)
4. Write unit tests for handlers/validators — tests MUST fail (RED phase)
5. Implement production code to make tests pass (GREEN phase)
6. Refactor while keeping tests green (REFACTOR phase)
7. Repeat for next behavior

Integration tests using `WebApplicationFactory` verify the full HTTP pipeline. Unit tests with xUnit + Shouldly verify handler logic. Reqnroll + Gherkin provide living documentation. NO implementation without failing tests first.

**Rationale:** Test-first ensures testable design, prevents over-engineering, provides regression safety, and produces executable specifications that serve as documentation.

### IV. Security by Identity
**Zero secrets, Managed Identity only.** Every agent and the Broker API MUST authenticate using Azure User-Assigned Managed Identities. NO secrets, NO API keys, NO certificates, NO passwords in code, config, or environment variables. Authorization MUST use Entra ID App Roles validated via JWT claims. The Broker API validates the `roles` claim on every request. Private endpoints and VNET integration are the enterprise upgrade path.

**Rationale:** Eliminates credential rotation, secret sprawl, and credential leakage risks. Managed Identity is Azure's recommended security pattern and aligns with zero-trust architecture.

### V. Modern C# Standards
**Leverage latest language features for safety and clarity.** All code MUST target .NET 9 (LTS) and C# 13 with:
- **Nullable reference types enabled** — all reference types explicitly nullable or non-nullable
- **File-scoped namespaces** — required
- **Primary constructors** — preferred for dependency injection
- **Records** — for all DTOs, requests, responses, events, value objects
- **`sealed` by default** — unless designed for inheritance
- **`TimeProvider` injection** — NEVER use `DateTime.UtcNow` directly (testability)
- **`CancellationToken` required** — on all async methods
- **Central Package Management** — `Directory.Packages.props` as single source of truth
- **EditorConfig enforcement** — `.editorconfig` rules verified in build (`EnforceCodeStyleInBuild=true`)

**Rationale:** Modern C# features improve type safety, reduce boilerplate, enhance testability, and prevent entire categories of bugs. Consistent standards reduce cognitive load.

### VI. Observability from Day 1
**Distributed tracing, structured logging, metrics — not optional.** Every service MUST emit:
- **Distributed traces** via OpenTelemetry SDK to Application Insights, correlated by `x-trace-id` header
- **Structured logs** via Serilog with correlation IDs
- **Custom metrics** (e.g., messages/sec, agents registered, DLQ depth)
- **Health endpoints** (`/health`, `/health/ready`) for liveness and readiness probes

All HTTP requests, Service Bus operations, and Cosmos DB queries MUST be traced. Logs MUST be structured JSON, not string interpolation.

**Rationale:** Production debugging without observability is impossible. Distributed tracing enables root cause analysis across service boundaries. Structured logging enables queryable insights.

### VII. Enterprise-Ready MVP
**Upgrade paths defined upfront, no technical debt.** The MVP MUST be production-ready with clear, documented paths to enterprise scale:
- Serverless-first (Container Apps Consumption, Cosmos DB Serverless, Service Bus Standard) to minimize idle cost
- WAF pillar compliance (Security, Reliability, Performance Efficiency, Cost Optimization, Operational Excellence)
- Infrastructure as Code (Bicep) for all resources — no manual provisioning
- CI/CD automation (GitHub Actions) for build, test, deploy
- Zero-downtime deployment support
- API versioning (`/api/v1/`) with documented breaking change policy

Enterprise upgrade paths (Service Bus Premium, Cosmos DB provisioned throughput, multi-region, APIM) MUST be documented in architecture. New features MUST respect these upgrade paths — no architectural dead ends.

**Rationale:** "We'll fix it later" creates technical debt that compounds. MVP code should be production code, not prototype code. Clear upgrade paths prevent costly rewrites at scale.

## Technology Stack Standards

### Mandatory Stack (MVP)
- **Runtime:** .NET 9 (LTS), C# 13, ASP.NET Core Minimal APIs
- **Data:** Azure Cosmos DB (NoSQL API, Serverless), Azure Service Bus (Standard)
- **Hosting:** Azure Container Apps (Consumption), Azure Container Registry
- **Identity:** Microsoft Entra ID, User-Assigned Managed Identities
- **Observability:** Azure Application Insights, OpenTelemetry SDK, Serilog
- **Testing:** xUnit 2.x, Shouldly, Reqnroll 2.x (Gherkin), NSubstitute, Bogus
- **IaC:** Bicep templates, Azure CLI for deployment
- **CI/CD:** GitHub Actions
- **Optional Dashboard:** React 19, TypeScript 5.x, Vite 6.x, TanStack Query 5.x, Tailwind CSS 4.x

### Technology Constraints
- NO third-party message brokers (RabbitMQ, Kafka) in MVP — Azure Service Bus only
- NO ORMs (Entity Framework) — use Cosmos DB SDK directly for transparency
- NO shared database anti-pattern — each module owns its data
- NO synchronous HTTP between modules — modules communicate via interfaces, not network calls
- NO global state or singletons (except DI-managed)

## Architecture Governance

### Module Boundaries
- Each module (Registration, Messaging, Eventing) MUST have its own `{Module}Module.cs` with `Add{Module}Module()` and `Map{Module}Endpoints()` methods
- Modules MUST NOT reference each other's internal types — only Shared Kernel interfaces
- Dependency direction: Modules → Shared Kernel (never reverse)
- Adding cross-module dependencies requires architecture review and documented justification

### Feature Implementation
- Each feature MUST reside in its own subdirectory: `Features/{FeatureName}/`
- Feature directory MUST contain: `{Feature}Endpoint.cs`, `{Feature}Request.cs`, `{Feature}Response.cs`, `{Feature}Handler.cs`, `{Feature}Validator.cs`
- Shared logic MUST be elevated to Shared Kernel ONLY when reused by 3+ features
- Feature handlers MUST accept `ClaimsPrincipal` and `CancellationToken` parameters

### Code Quality Gates
- **Build-time:** Code must compile with `TreatWarningsAsErrors=true`, pass EditorConfig rules, satisfy `Microsoft.CodeAnalysis.NetAnalyzers` and `StyleCop.Analyzers`
- **Test-time:** 100% of BDD scenarios must pass, unit test coverage for all handlers and validators
- **Deployment-time:** Health checks must pass, smoke tests against `/health` and `/health/ready`

### Breaking Change Policy
- API version prefix (`/api/v1/`) MUST be present in all routes
- Breaking changes (removed fields, changed types, removed endpoints) require new API version (`/api/v2/`)
- Non-breaking changes (new optional fields, new endpoints) can be added to existing version
- Deprecated endpoints MUST return `Deprecation` header and be supported for minimum 6 months

## Governance

This constitution supersedes all other development practices and architectural decisions. All pull requests, code reviews, design discussions, and feature implementations MUST verify compliance with these principles. Complexity must be justified against principles — "this is simpler elsewhere" is not sufficient justification without architectural rationale.

### Amendment Process
1. Propose amendment with concrete rationale (what changed, why principles are obsolete)
2. Document impact on existing code and templates
3. Obtain approval from architecture owner (requires documented justification)
4. Update constitution version per semantic versioning:
   - **MAJOR**: Backward-incompatible governance changes, principle removal/redefinition
   - **MINOR**: New principles added, material expansion of guidance
   - **PATCH**: Clarifications, wording, typo fixes, non-semantic refinements
5. Propagate changes to all affected templates (plan, spec, tasks)
6. Update runtime guidance in ARCHITECTURE.md if architectural patterns change

### Compliance Reviews
- Quarterly review: verify code aligns with principles
- Post-incident review: identify principle violations contributing to issues
- Pre-deployment review: verify constitution compliance checklist

### Reference Documentation
- **Architecture:** [docs/architecture/ARCHITECTURE.md](../../docs/architecture/ARCHITECTURE.md)
- **Templates:** `.specify/templates/` (plan, spec, tasks, constitution)

**Version**: 1.0.0 | **Ratified**: 2026-02-11 | **Last Amended**: 2026-02-11
