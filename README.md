# AgentBus - Autonomous Agent Message Broker

[![Build Status](https://github.com/m0x6A/agenthub/actions/workflows/build-test.yml/badge.svg)](https://github.com/m0x6A/agenthub/actions/workflows/build-test.yml)
[![Deploy Status](https://github.com/m0x6A/agenthub/actions/workflows/deploy-infra.yml/badge.svg)](https://github.com/m0x6A/agenthub/actions/workflows/deploy-infra.yml)

A production-ready message broker platform that enables autonomous agents to register, discover each other by capabilities, communicate via direct messaging, and publish/subscribe to domain events.

## Features

- **Agent Registration & Discovery**: Register agents with capabilities, discover agents by capability patterns
- **Direct Messaging**: Point-to-point message delivery with acknowledgment and dead-letter queues
- **Event Publishing & Subscription**: Pub/sub eventing with topic-based routing and SQL filters
- **Zero-Secrets Security**: Azure Managed Identity authentication with Entra ID JWT validation
- **Full Observability**: OpenTelemetry traces, structured logging, custom metrics, health checks
- **Infrastructure as Code**: Bicep templates for complete Azure deployment automation

## Architecture

- **Language**: C# 13 / .NET 9 (LTS)
- **Platform**: Azure Container Apps (Consumption plan)
- **Storage**: Azure Cosmos DB (Serverless) for agent registry, Azure Service Bus (Standard) for messaging
- **Authentication**: Azure Entra ID with User-Assigned Managed Identities
- **Observability**: OpenTelemetry + Application Insights, Serilog structured logging

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Docker](https://docs.docker.com/get-docker/) (for containerized deployment)
- Azure subscription with Contributor access

### Quick Start

See [specs/001-deployable-mvp/quickstart.md](specs/001-deployable-mvp/quickstart.md) for detailed getting started guide.

### Build and Test Locally

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test

# Run locally (requires Azure services or emulators)
dotnet run --project src/AgentBus.Broker
```

### Deploy to Azure

```bash
# Login to Azure
az login

# Deploy infrastructure
az deployment sub create \
  --location eastus \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam

# Build and push Docker image
docker build -t agentbus-broker:latest -f src/AgentBus.Broker/Dockerfile .
az acr login --name <your-acr-name>
docker tag agentbus-broker:latest <your-acr-name>.azurecr.io/agentbus-broker:latest
docker push <your-acr-name>.azurecr.io/agentbus-broker:latest
```

## Project Structure

```
AgentBus/
├── src/
│   └── AgentBus.Broker/              # Broker API (modular monolith)
│       ├── Program.cs                 # Minimal API bootstrapping
│       ├── SharedKernel/              # Cross-cutting concerns
│       └── Modules/                   # Feature modules
│           ├── Registration/          # Agent registration & discovery
│           ├── Messaging/             # Direct messaging
│           ├── Eventing/              # Pub/sub eventing
│           └── Health/                # Health checks
├── tests/
│   └── AgentBus.Broker.Tests/        # BDD scenarios + unit tests
├── infra/                            # Bicep infrastructure
│   ├── main.bicep
│   └── modules/
├── specs/                            # Feature specifications
│   └── 001-deployable-mvp/
└── docs/                             # Documentation
```

## API Endpoints

### Registration
- `POST /api/v1/agents` - Register agent
- `GET /api/v1/agents` - Discover agents by capability
- `GET /api/v1/agents/{id}` - Get agent by ID
- `PATCH /api/v1/agents/{id}/heartbeat` - Update heartbeat
- `DELETE /api/v1/agents/{id}` - Deregister agent

### Messaging
- `POST /api/v1/messages/send` - Send direct message
- `GET /api/v1/messages/receive` - Receive messages (long-polling)
- `POST /api/v1/messages/{id}/ack` - Acknowledge message

### Eventing
- `POST /api/v1/events/publish` - Publish event
- `POST /api/v1/events/subscribe` - Subscribe to event type
- `GET /api/v1/events/receive/{subscriptionId}` - Receive events
- `DELETE /api/v1/events/subscriptions/{id}` - Unsubscribe

### Health
- `GET /api/v1/health` - Health check (liveness)
- `GET /api/v1/health/ready` - Readiness check

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development workflow, code style, and PR guidelines.

## License

MIT License - see [LICENSE](LICENSE) for details.

## Support

For issues, questions, or contributions, please open an issue on GitHub.
