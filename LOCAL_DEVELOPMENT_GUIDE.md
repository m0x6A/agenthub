# Local Development Setup for AgentBus

This guide explains how to set up a complete local emulation environment for AgentBus development without requiring Azure subscriptions.

## Prerequisites

- **Docker Desktop** (Windows, Mac, or Linux) - [Download](https://www.docker.com/products/docker-desktop)
- **.NET 10.0 SDK** - [Download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- **Visual Studio Code** or **Visual Studio 2022** (or later)
- **Git**

## Quick Start

### 1. Start Local Services (Docker Compose)

From the workspace root:

```bash
docker-compose up -d
```

This starts:
- **Cosmos DB Emulator** on `https://localhost:8081`
- **Azure Service Bus Emulator** on `amqp://localhost:5672`

Wait 30-60 seconds for services to be fully ready.

### 2. Verify Services Are Running

```bash
# Check Docker status
docker-compose ps

# Test Cosmos DB (you should see an index page)
curl --insecure https://localhost:8081

# Test Service Bus
curl http://localhost:9600/health
```

### 3. Configure Local Development Settings

The project already includes `appsettings.Development.Local.json` with emulator connection strings.

In `Program.cs` or `appsettings.json`, ensure the app uses the local configuration:

```bash
# Set environment variable (in Windows PowerShell)
$env:ASPNETCORE_ENVIRONMENT = "Development"

# Or for bash/CMD
set ASPNETCORE_ENVIRONMENT=Development
```

### 4. Run AgentBus.Broker Locally

From the `src/AgentBus.Broker` directory:

```bash
# Restore packages
dotnet restore

# Run with local settings
dotnet run --environment=Development
```

The API should start at `https://localhost:5001` (or port shown in console).

### 5. Test the Service

```bash
# Health check (should return 200)
curl --insecure https://localhost:5001/api/v1/health

# Swagger UI
https://localhost:5001/swagger
```

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                   Your Local Machine                            │
│                                                                 │
│  ┌────────────────────────────┐                               │
│  │  AgentBus.Broker           │                               │
│  │  (dotnet run)              │                               │
│  │  localhost:5001            │                               │
│  └─────────┬──────────────────┘                               │
│            │                                                  │
│      ┌─────┴──────┬────────────┐                             │
│      │             │            │                             │
│      ▼             ▼            ▼                             │
│  ┌────────┐  ┌──────────┐  ┌──────────┐                     │
│  │ Cosmos │  │ Service  │  │   App    │                     │
│  │   DB   │  │   Bus    │  │ Insights │                     │
│  │ Emul.  │  │  Emul.   │  │  (mock)  │                     │
│  │:8081   │  │ :5672    │  │          │                     │
│  └────────┘  └──────────┘  └──────────┘                     │
│                                                               │
│  (All in Docker)                                            │
└─────────────────────────────────────────────────────────────┘
```

## Common Tasks

### Run Tests

```bash
cd tests/AgentBus.Broker.Tests
dotnet test
```

### Debug Cosmos DB Data

1. Download [Cosmos DB Data Explorer](https://cosmos.azure.com/), or
2. Use VS Code extension: **Azure Databases**

Connection string: `AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyZLgiCvqqPH+1DQtQ==;`

### Monitor Service Bus Messages

1. Download [Service Bus Explorer](https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.administration.servicebusadministrationclient), or
2. Use the built-in emulator dashboard at `http://localhost:9600`

### Stop All Services

```bash
docker-compose down

# Or with volume cleanup
docker-compose down -v
```

## Troubleshooting

### Cosmos DB Emulator Won't Start

- **Issue**: Port 8081 already in use
- **Fix**: 
  ```bash
  # Find process using 8081
  netstat -ano | findstr :8081
  # Kill process or change docker-compose port mapping
  ```

### Service Bus Emulator Connection Fails

- **Issue**: Connection string invalid
- **Fix**: Use the provided connection string in `appsettings.Development.Local.json`
- The emulator only works with the exact connection string format

### High Memory Usage

- Cosmos DB emulator typically uses 2-4 GB RAM
- Service Bus emulator uses ~500 MB
- Ensure Docker Desktop has sufficient memory allocation (Settings → Resources)

### Database/Queue Not Found

- Emulator starts with no data
- Run the application once to initialize containers
- Check logs: `docker-compose logs cosmosdb`

## Development Workflow

1. **Start emulators**: `docker-compose up -d`
2. **Run broker**: `dotnet run` from `src/AgentBus.Broker`
3. **Run tests**: `dotnet test` from tests folder
4. **Check logs**: `docker-compose logs -f`
5. **Stop**: `Ctrl+C` on broker, then `docker-compose down`

## Next Steps

- Review [TESTING_GUIDE.md](docs/TESTING_GUIDE.md) for BDD test setup
- Check [ARCHITECTURE.md](docs/architecture/ARCHITECTURE.md) for system design
- Explore API contracts in [openapi.yaml](specs/001-deployable-mvp/contracts/openapi.yaml)

## Additional Resources

- [Azure Cosmos DB Emulator Documentation](https://docs.microsoft.com/en-us/azure/cosmos-db/local-emulator)
- [Azure Service Bus Emulator Preview](https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-emulator-overview)
- [AgentBus Architecture](docs/architecture/ARCHITECTURE.md)
