using AgentBus.Examples.Shared;
using AgentBus.Examples.LogisticsUI;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.Services.AddSerilog((ctx, cfg) =>
    cfg.MinimumLevel.Information()
        .WriteTo.Console()
        .Enrich.FromLogContext());

// Add services
builder.Services.AddSingleton<AgentCommunicationService>();

// Create AgentBusClient that connects to broker
builder.Services.AddSingleton<IAgentBusClient>(sp =>
{
    var brokerUrl = builder.Configuration["AgentBusUrl"] ?? "http://localhost:5000";
    var logger = sp.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Connecting to AgentBus Broker at: {BrokerUrl}", brokerUrl);
    return new HttpAgentBusClient(brokerUrl, "logistics-ui");
});

// Add background service to listen to events
builder.Services.AddHostedService<EventListenerService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b =>
        b.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

// Add global exception handler for better error visibility
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception in request pipeline");

        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Internal Server Error",
                message = ex.Message,
                type = ex.GetType().Name
            });
        }
    }
});

// Enable default files so index.html is served automatically
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("AllowAll");

var communicationService = app.Services.GetRequiredService<AgentCommunicationService>();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Serve index.html - fallback for root
app.MapGet("/", async (HttpContext context) =>
{
    context.Response.ContentType = "text/html";
    var indexPath = Path.Combine(builder.Environment.WebRootPath ?? "wwwroot", "index.html");
    if (File.Exists(indexPath))
    {
        await context.Response.SendFileAsync(indexPath);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync($"index.html not found at {indexPath}");
    }
});

// API: Get all events
app.MapGet("/api/events", () =>
{
    var commService = app.Services.GetRequiredService<AgentCommunicationService>();
    var events = commService.GetEvents();
    return Results.Ok(new { events, count = events.Count });
});

// API: Stream events via Server-Sent Events
app.MapGet("/api/events/stream", async (HttpContext context) =>
{
    var commService = context.RequestServices.GetRequiredService<AgentCommunicationService>();

    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    var lastEventCount = 0;

    // Send existing events first
    var events = commService.GetEvents();
    foreach (var evt in events)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(evt);
        await context.Response.WriteAsync($"data: {json}\n\n");
        await context.Response.Body.FlushAsync();
    }

    lastEventCount = events.Count;

    // Keep connection alive and send new events
    while (!context.RequestAborted.IsCancellationRequested)
    {
        await Task.Delay(1000);

        var currentEvents = commService.GetEvents();
        if (currentEvents.Count > lastEventCount)
        {
            var newEvents = currentEvents.Skip(lastEventCount);
            foreach (var evt in newEvents)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(evt);
                await context.Response.WriteAsync($"data: {json}\n\n");
                await context.Response.Body.FlushAsync();
            }

            lastEventCount = currentEvents.Count;
        }
    }
});

// API: Trigger scenario 1 - Simple inquiry
app.MapPost("/api/scenarios/urgent-tokyo", async () =>
{
    try
    {
        var agentBus = app.Services.GetRequiredService<IAgentBusClient>();
        var commService = app.Services.GetRequiredService<AgentCommunicationService>();

        // Publish actual event that triggers real agents
        await agentBus.PublishEventAsync("global", new
        {
            requestId = "SR-" + Guid.NewGuid().ToString()[..8],
            productName = "Premium Electronics",
            quantity = 10,
            destination = "Tokyo",
            requiredHours = 24,
            customerTier = "VIP",
            budget = "flexible",
            urgency = "HIGH",
            notes =
                "Customer needs expedited delivery within 24 hours. Can consolidate from warehouses if needed to optimize costs."
        });

        return Results.Ok(new
        {
            scenario = "urgent-tokyo",
            status = "triggered",
            message = "Published global event - watch for agent responses!"
        });
    }
    catch (TimeoutException ex)
    {
        return Results.Json(new
        {
            scenario = "urgent-tokyo",
            status = "timeout",
            message = "Event publish timed out but may have been delivered. Check events panel.",
            error = ex.Message
        }, statusCode: 202); // Accepted
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            scenario = "urgent-tokyo",
            status = "error",
            message = "Failed to publish event",
            error = ex.Message
        }, statusCode: 500);
    }
});

// API: Trigger scenario 2 - Inventory constraint
app.MapPost("/api/scenarios/inventory-issue", async () =>
{
    try
    {
        var agentBus = app.Services.GetRequiredService<IAgentBusClient>();

        await agentBus.PublishEventAsync("global", new
        {
            requestId = "SR-" + Guid.NewGuid().ToString()[..8],
            productName = "Standard Widget",
            quantity = 100,
            destination = "Singapore",
            requiredHours = 48,
            customerTier = "Standard",
            budget = "standard",
            urgency = "MEDIUM",
            notes = "Need 100 units to Singapore within 48 hours. Check inventory availability across network."
        });

        return Results.Ok(new
        {
            scenario = "inventory-issue",
            status = "triggered",
            message = "Published global event - watch for agent responses!"
        });
    }
    catch (TimeoutException ex)
    {
        return Results.Json(new
        {
            scenario = "inventory-issue",
            status = "timeout",
            message = "Event publish timed out but may have been delivered. Check events panel.",
            error = ex.Message
        }, statusCode: 202);
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            scenario = "inventory-issue",
            status = "error",
            message = "Failed to publish event",
            error = ex.Message
        }, statusCode: 500);
    }
});

app.MapPost("/api/scenarios/cost-optimization", async () =>
{
    try
    {
        var agentBus = app.Services.GetRequiredService<IAgentBusClient>();

        await agentBus.PublishEventAsync("global", new
        {
            requestId = "SR-" + Guid.NewGuid().ToString()[..8],
            productName = "Bulk Order Widgets",
            quantity = 500,
            destination = "Melbourne",
            requiredHours = 168, // 7 days
            customerTier = "Enterprise",
            budget = "flexible",
            urgency = "LOW",
            notes =
                "Large bulk order. No rush. Looking for cost optimization opportunities through consolidation."
        });

        return Results.Ok(new
        {
            scenario = "cost-optimization",
            status = "triggered",
            message = "Published global event - watch for agent responses!"
        });
    }
    catch (TimeoutException ex)
    {
        return Results.Json(new
        {
            scenario = "cost-optimization",
            status = "timeout",
            message = "Event publish timed out but may have been delivered. Check events panel.",
            error = ex.Message
        }, statusCode: 202);
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            scenario = "cost-optimization",
            status = "error",
            message = "Failed to publish event",
            error = ex.Message
        }, statusCode: 500);
    }
});

app.MapPost("/api/events/clear", () =>
{
    // Events are managed by the background service, just return ok
    // In a real app, you'd clear the queue
    return Results.Ok(new { status = "cleared (restart to actually clear)" });
});

app.Run();
