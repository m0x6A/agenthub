# AgentBus Quickstart Guide

**Last Updated**: 2026-02-11  
**Feature**: 001-deployable-mvp  
**Prerequisites**: Azure subscription, Azure CLI, .NET 9 SDK, Docker (optional for local development)

## Overview

This guide walks you through:
1. Deploying AgentBus infrastructure to Azure (15 minutes)
2. Building and deploying the Broker API (10 minutes)
3. Registering your first agent (5 minutes)
4. Sending messages and publishing events (10 minutes)

**Total Time**: ~40 minutes from zero to working agent communication

---

## Prerequisites

### Required Tools

```bash
# Verify Azure CLI (version 2.50+)
az --version

# Verify .NET SDK (version 9.0+)
dotnet --version

# Verify Git
git --version
```

### Azure Permissions

You need an Azure subscription with:
- **Owner** or **Contributor** role for resource provisioning
- **Application Administrator** role in Entra ID for app registration and role assignments

### Azure CLI Login

```bash
# Login to Azure
az login

# Set default subscription
az account set --subscription "YOUR_SUBSCRIPTION_ID"

# Verify login
az account show
```

---

## Step 1: Clone Repository

```bash
# Clone AgentBus repository
git clone https://github.com/your-org/AgentBus.git
cd AgentBus
```

---

## Step 2: Deploy Infrastructure

### 2.1 Configure Deployment Parameters

Edit `infra/parameters/dev.bicepparam`:

```bicep
using '../main.bicep'

param environmentName = 'dev'
param location = 'eastus2'
param deploymentName = 'agentbus-dev'
param adminEmailAddress = '[email protected]'  // Your email
```

### 2.2 Deploy Bicep Templates

```bash
# Navigate to infrastructure directory
cd infra

# Create deployment (subscription scope)
az deployment sub create \
  --location eastus2 \
  --template-file main.bicep \
  --parameters parameters/dev.bicepparam \
  --name agentbus-dev-$(date +%Y%m%d-%H%M%S)
```

**Expected Output**:
```
Deployment completed successfully (12-15 minutes)
├── Resource Group: rg-agentbus-dev-eastus2
├── Cosmos DB: cosmos-agentbus-dev
├── Service Bus: sb-agentbus-dev
├── Container Apps Environment: cae-agentbus-dev
├── Container Registry: cracragentbusdev
├── Application Insights: appi-agentbus-dev
└── Managed Identities: id-agentbus-broker-dev, id-agentbus-agent-dev
```

### 2.3 Capture Deployment Outputs

```bash
# Get deployment outputs
az deployment sub show \
  --name agentbus-dev-TIMESTAMP \
  --query properties.outputs

# Save important values
export RESOURCE_GROUP="rg-agentbus-dev-eastus2"
export BROKER_API_URL="https://broker-api.nicegrass-12345678.eastus2.azurecontainerapps.io"
export ACR_NAME="cracragentbusdev"
```

### 2.4 Verify Infrastructure Health

```bash
# Check Cosmos DB
az cosmosdb show \
  --name cosmos-agentbus-dev \
  --resource-group $RESOURCE_GROUP \
  --query provisioningState

# Check Service Bus
az servicebus namespace show \
  --name sb-agentbus-dev \
  --resource-group $RESOURCE_GROUP \
  --query provisioningState

# Check Container Apps environment
az containerapp env show \
  --name cae-agentbus-dev \
  --resource-group $RESOURCE_GROUP \
  --query properties.provisioningState
```

---

## Step 3: Build and Deploy Broker API

### 3.1 Build Docker Image

```bash
# Navigate to repository root
cd ..

# Build Docker image for Broker API
docker build -t $ACR_NAME.azurecr.io/agentbus-broker:latest \
  -f src/AgentBus.Broker/Dockerfile .
```

### 3.2 Push Image to Azure Container Registry

```bash
# Login to ACR
az acr login --name $ACR_NAME

# Push image
docker push $ACR_NAME.azurecr.io/agentbus-broker:latest
```

### 3.3 Deploy Container App

```bash
# Update Container App with new image
az containerapp update \
  --name broker-api \
  --resource-group $RESOURCE_GROUP \
  --image $ACR_NAME.azurecr.io/agentbus-broker:latest
```

### 3.4 Verify Broker API Health

```bash
# Check health endpoint (no auth required)
curl $BROKER_API_URL/api/v1/health

# Expected response:
{
  "status": "healthy",
  "checks": {
    "cosmosDb": { "status": "healthy", "responseTime": 45.3 },
    "serviceBus": { "status": "healthy", "responseTime": 32.1 },
    "applicationInsights": { "status": "healthy", "responseTime": 18.7 }
  },
  "timestamp": "2026-02-11T10:30:00Z"
}
```

---

## Step 4: Register Your First Agent

### 4.1 Obtain JWT Token

For testing, use Azure CLI credentials to generate JWT token:

```bash
# Get access token for AgentBus API
export ACCESS_TOKEN=$(az account get-access-token \
  --resource https://agentbusdev.onmicrosoft.com/broker-api \
  --query accessToken -o tsv)

# Verify token (decode JWT)
echo $ACCESS_TOKEN | cut -d. -f2 | base64 -d | jq .
```

**Note**: In production, agents use User-Assigned Managed Identity. This is for quickstart testing only.

### 4.2 Register Agent via API

Create `register-agent.json`:

```json
{
  "name": "Weather Agent",
  "version": "1.0.0",
  "capabilities": ["fetch-forecast", "detect-alerts", "provide-recommendations"],
  "messageTypes": {
    "accepts": ["query.weather.*"],
    "emits": ["event.weather.alert-issued", "event.weather.forecast-updated"]
  },
  "metadata": {
    "owner": "[email protected]",
    "environment": "dev",
    "tags": ["weather", "iot"]
  }
}
```

Register agent:

```bash
# POST /api/v1/agents
curl -X POST $BROKER_API_URL/api/v1/agents \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d @register-agent.json

# Expected response (201 Created):
{
  "id": "weather-agent-v1-abc123",
  "name": "Weather Agent",
  "version": "1.0.0",
  "status": "active",
  "capabilities": ["fetch-forecast", "detect-alerts", "provide-recommendations"],
  "messageTypes": {
    "accepts": ["query.weather.*"],
    "emits": ["event.weather.alert-issued", "event.weather.forecast-updated"]
  },
  "identity": {
    "managedIdentityId": "/subscriptions/.../id-agentbus-agent-dev",
    "principalId": "12345678-1234-1234-1234-123456789abc",
    "tenantId": "87654321-4321-4321-4321-cba987654321"
  },
  "endpoints": {
    "inboxQueueName": "agent-weather-agent-v1-abc123-inbox",
    "healthCheckUrl": null
  },
  "metadata": {
    "owner": "[email protected]",
    "environment": "dev",
    "tags": ["weather", "iot"]
  },
  "timestamps": {
    "registeredAt": "2026-02-11T10:45:00Z",
    "lastHeartbeat": "2026-02-11T10:45:00Z"
  }
}
```

### 4.3 Verify Agent Registration

```bash
# GET /api/v1/agents/{agentId}
curl $BROKER_API_URL/api/v1/agents/weather-agent-v1-abc123 \
  -H "Authorization: Bearer $ACCESS_TOKEN"

# Discover agents by capability
curl "$BROKER_API_URL/api/v1/agents?capability=fetch-forecast" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

---

## Step 5: Send Direct Messages

### 5.1 Register Second Agent (Logistics Coordinator)

Create `register-logistics-agent.json`:

```json
{
  "name": "Logistics Coordinator",
  "version": "1.0.0",
  "capabilities": ["schedule-drone", "assign-route"],
  "messageTypes": {
    "accepts": ["command.logistics.*", "query.weather.*"],
    "emits": ["event.logistics.drone-scheduled"]
  },
  "metadata": {
    "owner": "[email protected]",
    "environment": "dev"
  }
}
```

```bash
# Register logistics agent
curl -X POST $BROKER_API_URL/api/v1/agents \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d @register-logistics-agent.json
```

### 5.2 Send Message from Logistics Agent to Weather Agent

Create `send-message.json`:

```json
{
  "to": "weather-agent-v1-abc123",
  "messageType": "query.weather.forecast",
  "payload": {
    "location": {
      "lat": 37.7749,
      "lon": -122.4194
    },
    "timeRange": {
      "start": "2026-02-11T12:00:00Z",
      "end": "2026-02-11T18:00:00Z"
    }
  },
  "correlationId": "corr-12345678-90ab-cdef-1234-567890abcdef",
  "ttl": "PT24H",
  "headers": {
    "x-trace-id": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    "x-agent-version": "1.0.0"
  }
}
```

```bash
# POST /api/v1/messages/send
curl -X POST $BROKER_API_URL/api/v1/messages/send \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d @send-message.json

# Expected response (202 Accepted):
{
  "messageId": "msg-550e8400-e29b-41d4-a716-446655440000"
}
```

### 5.3 Receive Message (as Weather Agent)

```bash
# GET /api/v1/messages/receive (long-polling, 30s timeout)
curl "$BROKER_API_URL/api/v1/messages/receive?timeout=30" \
  -H "Authorization: Bearer $ACCESS_TOKEN"

# Expected response (200 OK):
{
  "messageId": "msg-550e8400-e29b-41d4-a716-446655440000",
  "correlationId": "corr-12345678-90ab-cdef-1234-567890abcdef",
  "from": "logistics-coordinator-v1-xyz789",
  "to": "weather-agent-v1-abc123",
  "messageType": "query.weather.forecast",
  "timestamp": "2026-02-11T10:50:15Z",
  "ttl": "PT24H",
  "payload": {
    "location": { "lat": 37.7749, "lon": -122.4194 },
    "timeRange": {
      "start": "2026-02-11T12:00:00Z",
      "end": "2026-02-11T18:00:00Z"
    }
  },
  "replyTo": "agent-logistics-coordinator-v1-xyz789-inbox",
  "headers": {
    "x-trace-id": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    "x-agent-version": "1.0.0"
  }
}
```

### 5.4 Acknowledge Message

```bash
# POST /api/v1/messages/{messageId}/ack
curl -X POST $BROKER_API_URL/api/v1/messages/msg-550e8400-e29b-41d4-a716-446655440000/ack \
  -H "Authorization: Bearer $ACCESS_TOKEN"

# Expected response (204 No Content)
```

---

## Step 6: Publish and Subscribe to Events

### 6.1 Subscribe to Weather Events (as Logistics Agent)

Create `subscribe.json`:

```json
{
  "eventType": "events.weather.alert-issued",
  "filters": [
    "severity='high'",
    "region IN ('west-coast', 'central')"
  ]
}
```

```bash
# POST /api/v1/events/subscribe
curl -X POST $BROKER_API_URL/api/v1/events/subscribe \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d @subscribe.json

# Expected response (201 Created):
{
  "id": "sub-f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "agentId": "logistics-coordinator-v1-xyz789",
  "eventType": "events.weather.alert-issued",
  "serviceBusSubscriptionName": "sub-logistics-coordinator-a3b2c1",
  "filters": [
    "severity='high'",
    "region IN ('west-coast', 'central')"
  ],
  "createdAt": "2026-02-11T11:00:00Z"
}
```

### 6.2 Publish Weather Alert Event (as Weather Agent)

Create `publish-event.json`:

```json
{
  "eventType": "events.weather.alert-issued",
  "dataVersion": "1.0.0",
  "data": {
    "alertId": "alert-789",
    "severity": "high",
    "region": "west-coast",
    "description": "Severe thunderstorm warning",
    "affectedArea": {
      "coordinates": [[37.7749, -122.4194], [34.0522, -118.2437]]
    },
    "validUntil": "2026-02-11T18:00:00Z"
  },
  "headers": {
    "x-correlation-id": "corr-alert-789"
  }
}
```

```bash
# POST /api/v1/events/publish
curl -X POST $BROKER_API_URL/api/v1/events/publish \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d @publish-event.json

# Expected response (202 Accepted):
{
  "eventId": "evt-7c9e6679-7425-40de-944b-e07fc1f90ae7"
}
```

### 6.3 Receive Event (as Logistics Agent)

```bash
# GET /api/v1/events/receive/{subscriptionId}
curl "$BROKER_API_URL/api/v1/events/receive/sub-f47ac10b-58cc-4372-a567-0e02b2c3d479?timeout=30" \
  -H "Authorization: Bearer $ACCESS_TOKEN"

# Expected response (200 OK):
{
  "eventId": "evt-7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "eventType": "events.weather.alert-issued",
  "source": "weather-agent-v1-abc123",
  "timestamp": "2026-02-11T11:05:00Z",
  "dataVersion": "1.0.0",
  "data": {
    "alertId": "alert-789",
    "severity": "high",
    "region": "west-coast",
    "description": "Severe thunderstorm warning",
    "affectedArea": {
      "coordinates": [[37.7749, -122.4194], [34.0522, -118.2437]]
    },
    "validUntil": "2026-02-11T18:00:00Z"
  },
  "headers": {
    "x-correlation-id": "corr-alert-789",
    "x-trace-id": "00-7d9f6841-b8a9-4c2e-8f73-1a3e5b9d2c8f-01"
  }
}
```

---

## Step 7: Monitor and Observe

### 7.1 View Distributed Traces in Application Insights

```bash
# Open Application Insights in Azure Portal
az monitor app-insights component show \
  --app appi-agentbus-dev \
  --resource-group $RESOURCE_GROUP \
  --query "appId" -o tsv
```

**Navigate to**: Azure Portal → Application Insights → Transaction search

**Filter by trace ID**: `00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01`

**Expected Trace Flow**:
```
POST /api/v1/messages/send
  └─ Validate sender identity (Cosmos DB)
      └─ Lookup target agent (Cosmos DB)
          └─ Enqueue message (Service Bus)
              └─ GET /api/v1/messages/receive
                  └─ Receive from queue (Service Bus)
```

### 7.2 Query Custom Metrics

```bash
# Query Application Insights metrics using KQL
az monitor app-insights metrics show \
  --app appi-agentbus-dev \
  --resource-group $RESOURCE_GROUP \
  --metric customMetrics/agents_registered_total \
  --interval PT1M
```

**Available Metrics**:
- `agents_registered_total`: Total number of registered agents
- `messages_sent_per_second`: Message throughput
- `events_published_per_second`: Event throughput
- `dlq_depth`: Dead-letter queue depth (alerts if > 0)

### 7.3 View Structured Logs

**Navigate to**: Azure Portal → Application Insights → Logs

**Sample KQL Query**:
```kusto
traces
| where timestamp > ago(1h)
| where customDimensions.EventId == "AgentRegistered"
| project timestamp, message, customDimensions.AgentId, customDimensions.TraceId
| order by timestamp desc
```

---

## Step 8: Update Agent Heartbeat

Agents should send periodic heartbeats (every 1-2 minutes) to remain active:

```bash
# PATCH /api/v1/agents/{agentId}/heartbeat
curl -X PATCH $BROKER_API_URL/api/v1/agents/weather-agent-v1-abc123/heartbeat \
  -H "Authorization: Bearer $ACCESS_TOKEN"

# Expected response (204 No Content)
```

**Background Job**: Agents without heartbeat for > 5 minutes are marked `inactive` and excluded from discovery queries.

---

## Step 9: Cleanup (Optional)

### 9.1 Deregister Agents

```bash
# DELETE /api/v1/agents/{agentId}
curl -X DELETE $BROKER_API_URL/api/v1/agents/weather-agent-v1-abc123 \
  -H "Authorization: Bearer $ACCESS_TOKEN"

curl -X DELETE $BROKER_API_URL/api/v1/agents/logistics-coordinator-v1-xyz789 \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

### 9.2 Delete Azure Resources

```bash
# Delete entire resource group (CAUTION: irreversible)
az group delete \
  --name $RESOURCE_GROUP \
  --yes --no-wait
```

---

## Troubleshooting

### Issue: Health check returns unhealthy

**Solution**: Check dependency connectivity

```bash
# Check Cosmos DB connectivity from Container App
az containerapp exec \
  --name broker-api \
  --resource-group $RESOURCE_GROUP \
  --command "curl https://cosmos-agentbus-dev.documents.azure.com:443/_explorerV2"

# Check Service Bus connectivity
az servicebus namespace authorization-rule keys list \
  --resource-group $RESOURCE_GROUP \
  --namespace-name sb-agentbus-dev \
  --name RootManageSharedAccessKey
```

### Issue: 401 Unauthorized on API calls

**Solution**: Verify JWT token and roles

```bash
# Decode JWT token and check roles claim
echo $ACCESS_TOKEN | cut -d. -f2 | base64 -d | jq .roles

# Expected: ["AgentBus.Agent"] or ["AgentBus.Admin"]
```

### Issue: Message not received

**Solution**: Check Service Bus queue

```bash
# Check inbox queue message count
az servicebus queue show \
  --resource-group $RESOURCE_GROUP \
  --namespace-name sb-agentbus-dev \
  --name agent-weather-agent-v1-abc123-inbox \
  --query "countDetails.activeMessageCount"

# Check dead-letter queue
az servicebus queue show \
  --resource-group $RESOURCE_GROUP \
  --namespace-name sb-agentbus-dev \
  --name agent-weather-agent-v1-abc123-inbox \
  --query "countDetails.deadLetterMessageCount"
```

### Issue: Event not received

**Solution**: Verify subscription and filters

```bash
# List subscriptions on topic
az servicebus topic subscription list \
  --resource-group $RESOURCE_GROUP \
  --namespace-name sb-agentbus-dev \
  --topic-name events.weather.alert-issued

# Check subscription filters
az servicebus topic subscription rule list \
  --resource-group $RESOURCE_GROUP \
  --namespace-name sb-agentbus-dev \
  --topic-name events.weather.alert-issued \
  --subscription-name sub-logistics-coordinator-a3b2c1
```

---

## Next Steps

### Production Deployment

1. **Use GitHub Actions CI/CD**: See `.github/workflows/deploy-infra.yml`
2. **Configure Entra ID App Roles**: Assign `AgentBus.Agent` role to agent Managed Identities
3. **Enable HTTPS TLS 1.3**: Container Apps ingress configuration
4. **Configure Alerts**: Application Insights alert rules for DLQ depth, missed heartbeats
5. **Enable Autoscaling**: Configure Container Apps scaling rules (HTTP queue depth, CPU)

### Agent SDK Development

Create reusable SDK libraries for agents:
- `AgentBus.Sdk` NuGet package (C#)
- Auto-handle heartbeats in background thread
- Simplify message send/receive with strongly-typed wrappers
- Automatic retry with exponential backoff

### Enterprise Upgrades

- **Service Bus Premium**: VNET isolation, message sessions, geo-disaster recovery
- **Cosmos DB Provisioned Throughput**: Predictable performance, multi-region writes
- **Private Endpoints**: VNET-integrated agents with no public internet exposure
- **Azure API Management**: Rate limiting, quota enforcement, API versioning

---

## Reference Documentation

- **OpenAPI Spec**: [contracts/openapi.yaml](./contracts/openapi.yaml)
- **Data Model**: [data-model.md](./data-model.md)
- **Architecture**: [../../docs/architecture/ARCHITECTURE.md](../../docs/architecture/ARCHITECTURE.md)
- **Research**: [research.md](./research.md)
- **Constitution**: [../../.specify/memory/constitution.md](../../.specify/memory/constitution.md)

---

## Support

- **Issues**: GitHub Issues
- **Discussions**: GitHub Discussions
- **Email**: [email protected]
