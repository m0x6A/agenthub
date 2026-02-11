using AgentBus.Broker.SharedKernel.Security;
using AgentBus.Broker.SharedKernel.Telemetry;
using AgentBus.Broker.Modules.Registration;
using AgentBus.Broker.Modules.Messaging;
using AgentBus.Broker.Modules.Eventing;
using AgentBus.Broker.Modules.A2A;
using AgentBus.Broker.Modules.Communication;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.ApplicationInsights(
        builder.Configuration["ApplicationInsights:ConnectionString"] ?? string.Empty,
        TelemetryConverter.Traces)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

// Add OpenTelemetry
builder.Services.AddOpenTelemetryConfiguration(builder.Configuration);

// Add custom metrics
builder.Services.AddSingleton<AgentBusMetrics>();

// Add Azure services
builder.Services.AddSingleton(sp =>
{
    // Aspire injects Cosmos DB connection string as ConnectionStrings__cosmosdb
    var connectionString = builder.Configuration.GetConnectionString("cosmosdb") 
        ?? builder.Configuration["CosmosDb:ConnectionString"];
    var databaseName = builder.Configuration["CosmosDb:DatabaseName"] ?? "agentbus";
    
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Cosmos DB connection string is required. Configure via Aspire or CosmosDb:ConnectionString");
    }
    
    return new Microsoft.Azure.Cosmos.CosmosClient(connectionString);
});

builder.Services.AddSingleton(sp =>
{
    // Aspire injects Service Bus connection string as ConnectionStrings__servicebus
    var connectionString = builder.Configuration.GetConnectionString("servicebus")
        ?? builder.Configuration["ServiceBus:ConnectionString"];
    
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Service Bus connection string is required. Configure via Aspire or ServiceBus:ConnectionString");
    }
    
    return new Azure.Messaging.ServiceBus.ServiceBusClient(connectionString);
});

// Add modules
builder.Services.AddRegistrationModule();
builder.Services.AddMessagingModule();
builder.Services.AddEventingModule();
builder.Services.AddA2AModule();
builder.Services.AddCommunicationModule();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoints (no auth required)
app.MapGet("/api/v1/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("GetHealth")
    .WithTags("Health")
    .AllowAnonymous();

app.MapGet("/api/v1/health/ready", () => Results.Ok(new { status = "ready", timestamp = DateTime.UtcNow }))
    .WithName("GetReadiness")
    .WithTags("Health")
    .AllowAnonymous();

Log.Information("AgentBus.Broker starting up...");

// Initialize communication module
await app.InitializeCommunicationModuleAsync();

// Map module endpoints
app.MapRegistrationEndpoints();
app.MapMessagingEndpoints();
app.MapEventingEndpoints();
app.MapA2AEndpoints();
app.MapCommunicationEndpoints();

app.Run();

Log.Information("AgentBus.Broker shut down complete");
Log.CloseAndFlush();

// Make Program class visible for WebApplicationFactory in tests
public partial class Program { }
