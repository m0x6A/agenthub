# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY AgentBus.sln .
COPY Directory.Packages.props .
COPY Directory.Build.props .
COPY src/AgentBus.Broker/AgentBus.Broker.csproj src/AgentBus.Broker/

# Restore dependencies
RUN dotnet restore src/AgentBus.Broker/AgentBus.Broker.csproj

# Copy source code
COPY src/AgentBus.Broker/ src/AgentBus.Broker/

# Build and publish
WORKDIR /src/src/AgentBus.Broker
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/api/v1/health || exit 1

ENTRYPOINT ["dotnet", "AgentBus.Broker.dll"]
