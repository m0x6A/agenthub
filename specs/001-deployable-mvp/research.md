# Research: AgentBus Deployable MVP

**Date**: 2026-02-11  
**Feature**: 001-deployable-mvp  
**Purpose**: Document technology choices, architectural patterns, and best practices for MVP implementation

## Technology Selection Research

### 1. Azure Service Bus: Standard vs Premium Tier

**Decision**: Azure Service Bus Standard Tier

**Rationale**:
- **MVP Cost Optimization**: Standard tier pricing starts at ~$10/month base + message operations, while Premium starts at ~$670/month for dedicated messaging units
- **Sufficient Limits**: Standard tier supports 256 KB message size (adequate for agent payloads), 10 GB queue size, and unlimited topics/subscriptions
- **Feature Parity**: Standard includes all MVP requirements: dead-letter queues, scheduled delivery, duplicate detection, sessions, and RBAC with Managed Identity
- **Serverless Alignment**: Standard tier bill-per-operation model aligns with serverless-first architecture
- **Upgrade Path**: Premium tier provides clear upgrade path when requirements include: message size > 1 MB, predictable performance, VNET isolation, or geo-disaster recovery

**Alternatives Considered**:
- **Service Bus Premium**: Rejected for MVP due to cost ($670/month minimum). Reserved for enterprise scale when VNET isolation and dedicated throughput are required.
- **Azure Storage Queues**: Rejected - lacks pub/sub topics, no message sessions, limited to 64 KB messages, and requires polling instead of long-polling support
- **Third-party brokers (RabbitMQ, Kafka)**: Rejected - violates constitution principle of Azure-native services and increases operational complexity

**Implementation Notes**:
- Use `Azure.Messaging.ServiceBus` SDK v7.18+
- Configure dead-letter queues with max delivery count = 10
- Enable duplicate detection with 10-minute window
- Set default message TTL to 24 hours


### 2. Cosmos DB: Serverless vs Provisioned Throughput

**Decision**: Cosmos DB Serverless (NoSQL API)

**Rationale**:
- **Cost Efficiency**: Serverless has no minimum cost - pay only for consumed RU/s and storage (~$0.28 per million operations + $0.25/GB storage). Provisioned throughput requires minimum 400 RU/s reserved capacity (~$24/month).
- **MVP Workload Pattern**: Agent registry has bursty, unpredictable traffic (agent startups, periodic heartbeats, on-demand discovery). Serverless auto-scales from 0 to 5000 RU/s without pre-provisioning.
- **Development Simplicity**: No capacity planning, no throughput tuning, no reserved capacity management in MVP phase.
- **Upgrade Path**: Provisioned throughput provides clear upgrade path when: RU/s consistently exceeds 5000, multi-region writes required, or predictable performance SLAs needed.

**Alternatives Considered**:
- **Provisioned Throughput**: Rejected for MVP - requires upfront capacity planning, minimum $24/month cost even at zero traffic. Best for predictable, sustained workloads.
- **Azure SQL Database**: Rejected - relational model adds unnecessary schema rigidity for semi-structured agent metadata. Cosmos DB's flexible schema better suits evolving agent capabilities.
- **Azure Table Storage**: Rejected - lacks transactional guarantees, no change feed for future event sourcing, limited query capabilities (no secondary indexes).

**Implementation Notes**:
- Partition key: `/partitionKey` (synthetic key derived from agent ID for hot partition avoidance)
- Container: `agents` with unique key constraint on `/id`
- Indexing policy: Include all properties for discovery queries by capability
- Use `Microsoft.Azure.Cosmos` SDK v3.42+


### 3. Azure Container Apps: Consumption vs Dedicated Plans

**Decision**: Azure Container Apps (Consumption Plan)

**Rationale**:
- **True Serverless**: Consumption plan scales to zero when idle, paying only for active request time (~$0.000016 per vCPU-second). No idle costs unlike Dedicated plan's reserved capacity.
- **Automatic Scaling**: KEDA-based autoscaling responds to HTTP queue depth, CPU, memory metrics without pre-configuration. MVP benefits from elastic scale without capacity planning.
- **Managed Identity Native**: First-class support for User-Assigned Managed Identities with zero-configuration role assignment to Service Bus, Cosmos DB, Key Vault.
- **Dapr Integration**: Optional Dapr sidecar available for service-to-service auth, distributed tracing, and pub/sub abstraction (enterprise upgrade path).

**Alternatives Considered**:
- **Dedicated Plan**: Rejected for MVP - requires reserved compute capacity ($0.0775/vCPU-hour minimum), no scale-to-zero. Best for sustained high-traffic workloads.
- **Azure Functions**: Rejected - HTTP trigger model less suited for minimal API architecture. Container Apps provides full ASP.NET Core runtime flexibility.
- **Azure Kubernetes Service (AKS)**: Rejected - massive operational complexity, node pool management, cluster upgrades. AKS is enterprise upgrade when advanced orchestration needed.

**Implementation Notes**:
- Enable HTTP/2 ingress with TLS termination
- Configure health probes: liveness (`/health`), readiness (`/health/ready`)
- Set min replicas = 0, max replicas = 10 for MVP scale
- Use Bicep `Microsoft.App/containerApps` with managed environment


### 4. OpenTelemetry Distributed Tracing: Best Practices

**Decision**: OpenTelemetry SDK with Application Insights Exporter

**Rationale**:
- **Vendor-Neutral Standard**: OpenTelemetry is CNCF-graduated standard, ensuring no lock-in to Azure-specific tracing APIs. Can export to Jaeger, Zipkin, or Grafana Tempo if needed.
- **Auto-Instrumentation**: `OpenTelemetry.Instrumentation.AspNetCore` and `OpenTelemetry.Instrumentation.Http` automatically trace all HTTP requests/responses with zero code changes.
- **Azure Native Integration**: `Azure.Monitor.OpenTelemetry.Exporter` sends traces directly to Application Insights without custom exporters.
- **Correlation Across Services**: Propagates `x-trace-id` (W3C TraceContext) through Service Bus custom properties and Cosmos DB diagnostics for end-to-end correlation.

**Implementation Pattern**:
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation() // HTTP requests
        .AddHttpClientInstrumentation()  // Outbound HTTP
        .AddSource("Azure.*")            // Azure SDK activities
        .AddAzureMonitorTraceExporter()); // Export to App Insights
```

**Key Practices**:
- Add custom activities for business operations: `Activity.Current?.AddTag("agent.id", agentId)`
- Propagate trace context to Service Bus: `message.ApplicationProperties["traceparent"] = Activity.Current.Id`
- Use semantic conventions for span names: `HTTP POST /api/v1/agents` (not generic "HandleRequest")
- Configure sampling: 100% sampling for MVP (< 10 agents), reduce to 10% at scale


### 5. Managed Identity RBAC: Configuration Patterns

**Decision**: User-Assigned Managed Identity with Azure RBAC + Entra ID App Roles

**Rationale**:
- **User-Assigned Identity Advantages**: Can be shared across multiple Container App revisions, survives resource deletions, enables pre-provisioned role assignments before app deployment.
- **Zero Secrets**: Managed Identity eliminates all connection strings, API keys, and certificates. Credentials are handled by Azure platform with automatic token rotation.
- **Defense in Depth**: Combines Azure RBAC (data plane access to Service Bus/Cosmos) with Entra ID App Roles (API authorization logic).

**RBAC Role Assignments**:
| Resource | Role | Scope | Reason |
|----------|------|-------|--------|
| Service Bus Namespace | `Azure Service Bus Data Owner` | Namespace | Create/delete queues, send/receive messages |
| Cosmos DB Account | `Cosmos DB Built-in Data Contributor` | Account | Read/write agent documents, query by capability |
| Application Insights | `Monitoring Metrics Publisher` | Component | Emit custom metrics |

**App Roles for API Authorization**:
- `AgentBus.Agent`: Assigned to agent Managed Identities - can register, send messages, publish events
- `AgentBus.Admin`: Assigned to ops team identities - can query all agents, access dead-letter queues

**Implementation Notes**:
- Use `Microsoft.Identity.Web` middleware: `builder.Services.AddMicrosoftIdentityWebApiAuthentication()`
- Validate roles in endpoint filters: `[Authorize(Roles = "AgentBus.Agent")]`
- Configure Bicep: `Microsoft.ManagedIdentity/userAssignedIdentities` + `Microsoft.Authorization/roleAssignments`


### 6. Bicep Module Organization: Best Practices

**Decision**: Multi-file Bicep modules with central parameters file

**Rationale**:
- **Separation of Concerns**: Each Azure service (Service Bus, Cosmos DB, Container Apps, etc.) in own `.bicep` module promotes reusability and testability.
- **Environment-Specific Parameters**: `.bicepparam` files (`dev.bicepparam`, `prod.bicepparam`) externalize environment differences (SKUs, replica counts, regions).
- **Type Safety**: Bicep's strong typing catches configuration errors at authoring time vs runtime ARM template failures.
- **Dependency Management**: Implicit dependency detection (`resourceId()`, `reference()`) ensures correct deployment order without manual `dependsOn`.

**Module Structure**:
```
infra/
├── main.bicep                     # Orchestrator - composes all modules
├── modules/
│   ├── containerApps.bicep        # Container Apps environment + Broker API app
│   ├── cosmosDb.bicep             # Account, database, containers, indexes
│   ├── serviceBus.bicep           # Namespace, topics, authorization rules
│   ├── monitoring.bicep           # App Insights, Log Analytics workspace
│   ├── identity.bicep             # User-Assigned Managed Identities + RBAC
│   └── containerRegistry.bicep    # ACR with admin disabled, geo-replication
└── parameters/
    ├── dev.bicepparam             # Dev: smaller SKUs, single region
    └── prod.bicepparam            # Prod: Standard SKUs, zone redundancy
```

**Key Patterns**:
- Use `@description()` decorators on all parameters for auto-generated docs
- Return outputs from modules: `output serviceBusEndpoint string = serviceBus.properties.serviceBusEndpoint`
- Use Bicep functions for naming conventions: `name: 'agentbus-${environmentName}-${locationAbbreviation}'`
- Enable `what-if` previews: `az deployment sub what-if --template-file main.bicep`


### 7. GitHub Actions: Container App Deployment Strategy

**Decision**: Multi-stage workflow with Bicep deployment + image push + Container App revision

**Rationale**:
- **Separation of Infrastructure and Application**: Bicep handles Azure resource provisioning (infrequent), Docker build/push handles application updates (frequent). Enables independent deployment cadences.
- **Blue-Green Deployments**: Container Apps revisions enable zero-downtime deployments - new revision receives traffic only after health checks pass.
- **GitHub OIDC Auth**: Federated identity credentials eliminate long-lived service principal secrets in GitHub secrets.

**Workflow Stages**:
1. **Build & Test**: Restore NuGet packages → Build solution → Run xUnit + Reqnroll tests → Fail if tests fail
2. **Infrastructure Deploy** (on `main` branch): Authenticate with Azure OIDC → Deploy Bicep (`az deployment sub create`) → Capture outputs (ACR name, Container App name)
3. **Docker Build & Push**: Build image from Dockerfile → Tag with git SHA → Push to Azure Container Registry
4. **Container App Update**: Update Container App revision with new image tag → Enable ingress traffic after health check 200 OK

**Security Best Practices**:
- Use `az login --service-principal --federated-token` (no secrets)
- Store only non-sensitive config in GitHub Vars (subscription ID, resource group name)
- Inject secrets as Container App environment variables from Azure Key Vault references (enterprise upgrade)

**Example Workflow**:
```yaml
name: Deploy Broker API
on:
  push:
    branches: [main]
  pull_request: # Run tests only

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --verbosity normal

  deploy-infra:
    needs: build-test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    permissions:
      id-token: write # OIDC token
    steps:
      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      - run: |
          az deployment sub create \
            --location eastus2 \
            --template-file infra/main.bicep \
            --parameters infra/parameters/prod.bicepparam
```


## Architecture Patterns Research

### Modular Monolith Module Communication

**Pattern**: Shared Kernel interfaces with in-process method calls

**Rationale**: Modules in a modular monolith should communicate through well-defined interfaces (contracts) without HTTP or message queue overhead. This provides performance of a monolith with boundaries of microservices.

**Implementation**:
```csharp
// SharedKernel/Interfaces/IAgentRegistry.cs
public interface IAgentRegistry
{
    Task<Agent?> GetAgentByIdAsync(string agentId, CancellationToken ct);
    Task<IEnumerable<Agent>> FindAgentsByCapabilityAsync(string capability, CancellationToken ct);
}

// Modules/Registration/AgentRegistry.cs (internal implementation)
internal sealed class AgentRegistry : IAgentRegistry { /* Cosmos DB logic */ }

// Modules/Registration/RegistrationModule.cs
public static IServiceCollection AddRegistrationModule(this IServiceCollection services)
{
    services.AddSingleton<IAgentRegistry, AgentRegistry>();
    return services;
}

// Modules/Messaging/Features/SendMessage/SendMessageHandler.cs
public sealed class SendMessageHandler(IAgentRegistry registry) // Injected via interface
{
    public async Task<SendMessageResponse> HandleAsync(SendMessageRequest req, CancellationToken ct)
    {
        var targetAgent = await registry.GetAgentByIdAsync(req.ToAgentId, ct); // In-process call
        // ...
    }
}
```

**Benefits**:
- Zero network latency between modules
- Type-safe contracts at compile time
- Easy to extract module to microservice later (replace interface with HTTP client)
- Testable via mocking interfaces with NSubstitute


### Vertical Slice Feature Scaffold

**Pattern**: Self-contained feature with dedicated endpoint/request/response/handler/validator

**Example Feature**: `RegisterAgent` in Registration module

```
Features/RegisterAgent/
├── RegisterAgentEndpoint.cs         # Minimal API route definition
├── RegisterAgentRequest.cs          # Input DTO (record)
├── RegisterAgentResponse.cs         # Output DTO (record)
├── RegisterAgentHandler.cs          # Business logic + persistence
└── RegisterAgentValidator.cs        # FluentValidation rules
```

**Code Example**:
```csharp
// RegisterAgentRequest.cs
public sealed record RegisterAgentRequest(
    string Name,
    string Version,
    string[] Capabilities,
    MessageTypes MessageTypes);

// RegisterAgentEndpoint.cs
public static class RegisterAgentEndpoint
{
    public static void MapRegisterAgent(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/agents", HandleAsync)
            .RequireAuthorization(policy => policy.RequireRole("AgentBus.Agent"))
            .Produces<RegisterAgentResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleAsync(
        RegisterAgentRequest request,
        RegisterAgentHandler handler,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var response = await handler.HandleAsync(request, user, ct);
        return Results.Created($"/api/v1/agents/{response.AgentId}", response);
    }
}
```


### BDD Test-First Development Workflow

**Pattern**: Outside-in development starting with Gherkin scenarios

**Workflow**:
1. Write `.feature` file describing behavior in Given-When-Then format
2. Obtain user/stakeholder approval of acceptance criteria
3. Generate step definitions with Reqnroll (tests fail - RED phase)
4. Implement production code to make tests pass (GREEN phase)
5. Refactor while keeping tests green (REFACTOR phase)

**Example Gherkin**:
```gherkin
# Features/Registration.feature
Feature: Agent Registration
  As an autonomous agent
  I want to register with the AgentBus broker
  So that I can participate in agent-to-agent communication

  Scenario: Successfully register new agent
    Given I am an authenticated agent with Managed Identity
    And I have valid agent metadata:
      | Field        | Value                          |
      | Name         | logistics-coordinator-v1       |
      | Version      | 1.0.0                          |
      | Capabilities | schedule-drone, assign-route   |
    When I POST to "/api/v1/agents"
    Then I should receive a 201 Created response
    And the response should contain my agent ID
    And the response should contain my inbox queue name
    And I should be able to retrieve my registration via GET
```

**Step Definition with Integration Test**:
```csharp
[Binding]
public sealed class RegistrationSteps(WebApplicationFactory<Program> factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private HttpResponseMessage? _response;

    [Given("I am an authenticated agent with Managed Identity")]
    public void GivenAuthenticatedAgent()
    {
        // Setup JWT token with AgentBus.Agent role
        var token = TokenGenerator.CreateValidToken(roles: ["AgentBus.Agent"]);
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);
    }

    [When(@"I POST to ""(.*)""")]
    public async Task WhenIPostTo(string endpoint)
    {
        var request = new RegisterAgentRequest(/* ... */);
        _response = await _client.PostAsJsonAsync(endpoint, request);
    }

    [Then(@"I should receive a (\d+) (.*) response")]
    public void ThenIShouldReceiveResponse(int statusCode, string statusText)
    {
        _response.ShouldNotBeNull();
        ((int)_response.StatusCode).ShouldBe(statusCode);
    }
}
```


## Security Research

### Zero-Secrets Architecture Verification

**Validation Checklist**:
- ✅ No connection strings in `appsettings.json` or environment variables
- ✅ No API keys or SAS tokens in code
- ✅ No certificates or PFX files in repo
- ✅ Azure SDK clients use `DefaultAzureCredential` (resolves to Managed Identity in production)
- ✅ JWT validation uses `Microsoft.Identity.Web` with Entra ID issuer + audience validation
- ✅ All Service Bus operations use `ServiceBusClient(fullyQualifiedNamespace, credential: new DefaultAzureCredential())`
- ✅ All Cosmos DB operations use `CosmosClient(accountEndpoint, credential: new DefaultAzureCredential())`

**Local Development Pattern**:
```csharp
// Use Azure CLI login for local development, Managed Identity in production
builder.Services.AddSingleton<TokenCredential>(_ =>
    new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ExcludeEnvironmentCredential = true, // No env vars
        ExcludeVisualStudioCredential = true, // Only Azure CLI
        ExcludeSharedTokenCacheCredential = true
    }));
```


## Performance Considerations

### Cosmos DB Query Optimization

**Pattern**: Use partition key in all point reads

```csharp
// ❌ BAD: Cross-partition query (expensive, slow)
var agent = await container.GetItemQueryIterator<Agent>(
    "SELECT * FROM c WHERE c.id = 'agent-123'");

// ✅ GOOD: Point read with partition key (1 RU)
var agent = await container.ReadItemAsync<Agent>(
    id: "agent-123",
    partitionKey: new PartitionKey("agent-123")); // Same as ID for single-tenant
```

**Indexing Strategy**:
- Include `/capabilities/*` for capability discovery queries
- Exclude `/metadata/*` to reduce index size (not queried)


### Service Bus Long-Polling

**Pattern**: Use `ReceiveMessagesAsync` with max wait time for efficient message consumption

```csharp
// ❌ BAD: Busy polling (wastes CPU, increases RU consumption)
while (true)
{
    var messages = await receiver.ReceiveMessagesAsync(maxMessages: 1, maxWaitTime: TimeSpan.Zero);
    if (messages.Any()) break;
    await Task.Delay(100); // Polling loop
}

// ✅ GOOD: Long-polling (blocks until message arrives, max 60s timeout)
var messages = await receiver.ReceiveMessagesAsync(
    maxMessages: 1,
    maxWaitTime: TimeSpan.FromSeconds(30)); // Blocks up to 30s
```


## Cost Optimization Research

### Serverless Cost Modeling

**MVP Monthly Cost Estimate** (10 agents, 1000 messages/day):

| Service | SKU/Tier | Usage | Cost |
|---------|----------|-------|------|
| Container Apps | Consumption | 10 active hours/month × 0.5 vCPU × 1GB RAM | ~$1 |
| Cosmos DB | Serverless | 50k operations × $0.28/M RU/s + 1 GB storage | ~$0.25 |
| Service Bus | Standard | 1M operations × $0.05/M | ~$10 |
| Application Insights | Pay-as-you-go | 1 GB ingestion × $2.88/GB | ~$3 |
| Container Registry | Basic | 10 GB storage | ~$5 |
| **Total** | | | **~$20/month** |

**Enterprise Cost Estimate** (100 agents, 100k messages/day):
- Container Apps (Dedicated): 2 vCPU × 4 GB × 730 hours = ~$113/month
- Cosmos DB (Provisioned 1000 RU/s): ~$58/month
- Service Bus (Premium 1 MU): ~$670/month
- **Total**: **~$850/month**


## References

- [Azure Service Bus documentation](https://learn.microsoft.com/azure/service-bus-messaging/)
- [Cosmos DB serverless](https://learn.microsoft.com/azure/cosmos-db/serverless)
- [Container Apps overview](https://learn.microsoft.com/azure/container-apps/overview)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- [Managed identities for Azure resources](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/)
- [Bicep documentation](https://learn.microsoft.com/azure/azure-resource-manager/bicep/)
- [Vertical Slice Architecture](https://www.jimmybogard.com/vertical-slice-architecture/)
- [Modular Monolith pattern](https://www.kamilgrzybek.com/blog/posts/modular-monolith-primer)
