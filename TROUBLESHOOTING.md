# Local Development Troubleshooting Guide

## Common Issues & Solutions

### 🐳 Docker Issues

#### Docker Daemon Not Running
**Problem:** `Cannot connect to Docker daemon`
```
docker: Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?
```

**Solution:**
- **Windows**: Start Docker Desktop from the Start menu
- **Mac**: Start Docker Desktop from Applications/Docker.app
- **Linux**: 
  ```bash
  sudo systemctl start docker
  sudo systemctl enable docker  # Auto-start on boot
  ```

#### Port Already in Use
**Problem:** Services won't start due to port conflicts
```
Error response from daemon: Ports are not available: exposing port TCP 0.0.0.0:8081
```

**Solution:**
1. Find what's using the port:
   ```bash
   # Windows (PowerShell)
   netstat -ano | findstr :8081
   
   # Linux/Mac
   lsof -i :8081
   ```

2. Option A: Kill the process
   ```bash
   # Windows (PowerShell)
   taskkill /PID <PID> /F
   
   # Linux/Mac
   kill -9 <PID>
   ```

3. Option B: Change the port in `docker-compose.yml`
   ```yaml
   cosmosdb:
     ports:
       - "8082:8081"  # Change host port to 8082
   ```

#### Out of Disk Space
**Problem:** `no space left on device`

**Solution:**
```bash
# Clean up Docker
docker system prune -a --volumes
```

---

### 🗄️ Cosmos DB Emulator Issues

#### Cannot Connect to Localhost:8081
**Problem:** 
```
HttpRequestException: A connection attempt failed because the connected party
did not properly respond after a period of time...
```

**Solution:**
1. Check if container is running:
   ```bash
   docker-compose ps
   ```
   Look for `agentbus-cosmosdb` status

2. Verify it's healthy:
   ```bash
   curl --insecure https://localhost:8081/
   ```

3. Check logs:
   ```bash
   docker-compose logs cosmosdb
   ```

4. Wait longer for startup (can take 60+ seconds)

#### Invalid Connection String
**Problem:** `AuthorizationTokenExpired` or `InvalidToken`

**Solution:** Ensure you're using the exact connection string from `appsettings.Development.Local.json`:
```
AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyZLgiCvqqPH+1DQtQ==;
```

#### High Memory Usage
**Problem:** Cosmos DB using 3GB+ RAM

**Solution:**
- This is normal for the emulator
- Allocate more RAM to Docker Desktop (Settings → Resources → Memory)
- Consider using lighter testing approaches if memory is constrained

---

### 🚌 Service Bus Emulator Issues

#### Cannot Connect to Service Bus
**Problem:**
```
ServiceBusException: Couldn't establish a connection to localhost:5672
```

**Solution:**
1. Verify emulator is running:
   ```bash
   curl http://localhost:9600/health
   ```

2. Check the connection string in `appsettings.Development.Local.json`:
   ```
   Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=localkey==;UseDevelopmentEmulator=true;
   ```

3. Service Bus emulator is in preview - restart if issues persist:
   ```bash
   docker-compose restart servicebus
   ```

#### Queues/Topics Not Found
**Problem:** Queue or topic doesn't exist when application tries to use it

**Solution:** 
- The emulator doesn't auto-create resources
- Ensure your app creates them on startup
- Or manually create via Service Bus Explorer if using the dashboard

---

### 📦 .NET / NuGet Issues

#### Package Restore Fails
**Problem:**
```
error NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json
```

**Solution:**
1. Check internet connection
2. Clear NuGet cache:
   ```bash
   dotnet nuget locals all --clear
   ```
3. Try restore again:
   ```bash
   dotnet restore --verbosity detailed
   ```

#### Build Fails with "Type or namespace not found"
**Problem:** Compiler can't find classes/namespaces

**Solution:**
1. Ensure all projects are restored:
   ```bash
   dotnet restore
   ```
2. Clean build artifacts:
   ```bash
   dotnet clean
   dotnet build
   ```

#### Port 5001 Already in Use
**Problem:**
```
System.IO.IOException: Failed to bind to address address already in use
```

**Solution:**
1. Kill the process or change the port:
   ```bash
   # Windows (PowerShell)
   $env:ASPNETCORE_URLS="https://localhost:5002;http://localhost:5000"
   
   # Linux/Mac
   export ASPNETCORE_URLS="https://localhost:5002;http://localhost:5000"
   ```

2. Then run:
   ```bash
   dotnet run
   ```

---

### 🔐 Authentication Issues (Local Development)

#### JWT Validation Fails
**Problem:**
```
Exception: IDX10209: No SecurityTokenHandler can claim to validate this token
```

**Solution:**
Local development uses empty/dummy tokens. In `appsettings.Development.Local.json`, these are set to placeholder values:
```json
"AzureAd": {
  "TenantId": "00000000-0000-0000-0000-000000000000",
  "ClientId": "00000000-0000-0000-0000-000000000000"
}
```

To skip auth in development:
- If your middleware supports it, use `.AllowAnonymous()` for local
- Or test with valid Azure Entra ID credentials if needed

---

### 📊 Application Insights (Local)

#### Application Insights Won't Connect
**Problem:** Telemetry not being sent

**Solution:**
For local development, Application Insights is mocked:
```json
"ApplicationInsights": {
  "ConnectionString": "InstrumentationKey=00000000-0000-0000-0000-000000000000"
}
```

This is intentional - no Azure subscription needed. Logs go to console instead.

---

## Debugging Tips

### Enable Detailed Logging

Update `appsettings.Development.Local.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.Azure.Cosmos": "Debug",
      "Azure.Messaging.ServiceBus": "Debug"
    }
  }
}
```

### View Container Logs in Real-Time
```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f cosmosdb
docker-compose logs -f servicebus
```

### Inspect Docker Container
```bash
# Open shell in running container
docker-compose exec cosmosdb /bin/bash

# View container stats
docker stats
```

### Clean Start (Nuclear Option)
```bash
# Stop everything
docker-compose down -v

# Remove volumes (data loss!)
docker system prune -a --volumes

# Start fresh
docker-compose up -d
```

---

## Performance Optimization

### Reduce Memory Usage
```yaml
# In docker-compose.yml, add resource limits:
cosmosdb:
  deploy:
    resources:
      limits:
        memory: 2G
  servicebus:
    deploy:
      resources:
        limits:
          memory: 512M
```

### Faster Startup
1. Pre-pull images:
   ```bash
   docker pull mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
   docker pull mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
   ```

2. Use SSD for Docker volumes

3. Disable unnecessary log drivers

---

## Getting Help

1. Check [LOCAL_DEVELOPMENT_GUIDE.md](LOCAL_DEVELOPMENT_GUIDE.md)
2. Review [docs/IMPLEMENTATION_SUMMARY.md](docs/IMPLEMENTATION_SUMMARY.md)
3. Check Azure Emulator official docs:
   - [Cosmos DB Emulator](https://docs.microsoft.com/en-us/azure/cosmos-db/local-emulator)
   - [Service Bus Emulator](https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-emulator-overview)
4. Open an issue on GitHub with:
   - Output of `docker-compose ps`
   - Output of `docker-compose logs` (last 50 lines)
   - Your OS and Docker version (`docker --version`)
   - Steps to reproduce

---

## Quick Command Reference

```bash
# Services
docker-compose up -d          # Start services
docker-compose down           # Stop services
docker-compose logs -f        # View logs
docker-compose ps             # Service status

# .NET
dotnet restore               # Restore packages
dotnet build                 # Build solution
dotnet run                   # Run broker
dotnet test                  # Run tests
dotnet clean                 # Clean artifacts

# Testing
curl --insecure https://localhost:5001/api/v1/health
curl http://localhost:9600/health
```
