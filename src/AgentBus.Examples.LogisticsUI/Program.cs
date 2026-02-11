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

builder.Services.AddSingleton<IAgentBusClient>(sp =>
{
    var brokerUrl = builder.Configuration["AgentBusUrl"] ?? "http://localhost:5000";
    return new HttpAgentBusClient(brokerUrl, "logistics-ui");
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b =>
        b.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader());
});

var app = builder.Build();

app.UseStaticFiles();
app.UseCors("AllowAll");

var communicationService = app.Services.GetRequiredService<AgentCommunicationService>();

// Serve index.html
app.MapGet("/", () => Results.File("wwwroot/index.html", "text/html"));

// API: Get all messages
app.MapGet("/api/messages", () =>
{
    var messages = communicationService.GetMessages();
    return Results.Ok(new { messages });
});

// API: Stream messages via Server-Sent Events
app.MapGet("/api/events/stream", async (HttpContext context) =>
{
    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    var messages = communicationService.GetMessages();
    foreach (var msg in messages)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(msg);
        await context.Response.WriteAsync($"data: {json}\n\n");
        await context.Response.Body.FlushAsync();
    }

    // Keep connection alive for new messages
    while (!context.RequestAborted.IsCancellationRequested)
    {
        await Task.Delay(500);
    }
});

// API: Trigger scenario 1 - Simple inquiry
app.MapPost("/api/scenarios/urgent-tokyo", async (HttpContext context) =>
{
    var commService = app.Services.GetRequiredService<AgentCommunicationService>();
    var conversationId = Guid.NewGuid().ToString();
    
    commService.StartConversation(conversationId);

    // Simulate agent communication
    await Task.Delay(500);
    commService.LogMessage("shipping-agent", "warehouse-lead-agent", 
        "Hi! I received an urgent order for Tokyo delivery within 24 hours. The customer is VIP. " +
        "Standard expedited shipping costs $3,500. Can we consolidate inventory from regional hubs " +
        "to save costs? What would be the time and cost impact on your end?",
        "SENT");

    await Task.Delay(1000);
    commService.LogMessage("warehouse-lead-agent", "shipping-agent",
        "Got it. Checking inventory... We have 85% in Sydney local warehouse, 15% in Osaka. " +
        "Consolidation takes 4 hours total. Cost impact: $250 labor + $150 handling = $400 additional. " +
        "My recommendation: consolidate. Total cost to you: $3,900. That's still good for VIP express.",
        "RECEIVED");

    await Task.Delay(1000);
    commService.LogMessage("shipping-agent", "warehouse-lead-agent",
        "Perfect! That's within budget. Booking express carrier now. Can your team be ready for " +
        "2 PM Sydney pickup?",
        "SENT");

    await Task.Delay(800);
    commService.LogMessage("warehouse-lead-agent", "shipping-agent",
        "Confirmed! Team staged and ready by 2 PM. Coordinating with Osaka for the 15% portion. " +
        "Everything will be consolidated and ready for 6 PM departure.",
        "RECEIVED");

    commService.EndConversation(conversationId);
    
    return Results.Ok(new { 
        scenario = "urgent-tokyo",
        conversationId,
        status = "completed",
        messages = commService.GetMessages()
    });
});

// API: Trigger scenario 2 - Inventory constraint
app.MapPost("/api/scenarios/inventory-issue", async (HttpContext context) =>
{
    var commService = app.Services.GetRequiredService<AgentCommunicationService>();
    var conversationId = Guid.NewGuid().ToString();
    
    commService.StartConversation(conversationId);

    await Task.Delay(500);
    commService.LogMessage("shipping-agent", "warehouse-lead-agent",
        "Need help with an order: 100 units to Singapore (48-hour deadline). " +
        "Standard shipping is $800. But I'm wondering if we have inventory " +
        "available across our network?",
        "SENT");

    await Task.Delay(1000);
    commService.LogMessage("warehouse-lead-agent", "shipping-agent",
        "Checking inventory... We have a problem: Only 40 units in Sydney, " +
        "35 in Melbourne, 15 in Singapore hub already. Total 90 units, but you need 100. " +
        "We're short 10 units. Options: (1) Wait 2 days for restock, (2) Source from " +
        "external supplier (+$300), or (3) Ship partial order now, backorder 10 units.",
        "RECEIVED");

    await Task.Delay(1000);
    commService.LogMessage("shipping-agent", "warehouse-lead-agent",
        "Can't wait—48-hour deadline is firm. What's the timeline for external supplier? " +
        "And what's your recommendation?",
        "SENT");

    await Task.Delay(800);
    commService.LogMessage("warehouse-lead-agent", "shipping-agent",
        "External supplier can deliver 10 units in 24 hours. Cost $300. " +
        "My recommendation: Ship the 90 units now, add the 10 from supplier when ready. " +
        "Total additional cost $300. Customer gets delivery on time, receives remainder " +
        "in 2 days.",
        "RECEIVED");

    commService.EndConversation(conversationId);

    return Results.Ok(new {
        scenario = "inventory-issue",
        conversationId,
        status = "completed",
        messages = commService.GetMessages()
    });
});

// API: Trigger scenario 3 - Cost optimization
app.MapPost("/api/scenarios/cost-optimization", async (HttpContext context) =>
{
    var commService = app.Services.GetRequiredService<AgentCommunicationService>();
    var conversationId = Guid.NewGuid().ToString();
    
    commService.StartConversation(conversationId);

    await Task.Delay(500);
    commService.LogMessage("shipping-agent", "warehouse-lead-agent",
        "Large order incoming: 500 units to Melbourne (originating from Sydney warehouse). " +
        "Normally I'd ship direct, but volume is high. Could we batch consolidate with " +
        "other pending orders to reduce per-unit shipping cost?",
        "SENT");

    await Task.Delay(1000);
    commService.LogMessage("warehouse-lead-agent", "shipping-agent",
        "Good thinking! I'm checking pending orders... Yes! We have two other shipments " +
        "to Melbourne scheduled for this week (200 units and 150 units). We can consolidate " +
        "all three into one larger shipment. Savings: $400 on your order alone (negotiate " +
        "bulk rate with carrier).",
        "RECEIVED");

    await Task.Delay(1000);
    commService.LogMessage("shipping-agent", "warehouse-lead-agent",
        "Excellent! That works perfectly. Consolidation adds no time since we're not pushing " +
        "any deadline. When can you batch everything?",
        "SENT");

    await Task.Delay(800);
    commService.LogMessage("warehouse-lead-agent", "shipping-agent",
        "All ready by EOD tomorrow. Single consolidated pallet. Pickup at 2 PM. " +
        "Saved us all some money here!",
        "RECEIVED");

    commService.EndConversation(conversationId);

    return Results.Ok(new {
        scenario = "cost-optimization",
        conversationId,
        status = "completed",
        messages = commService.GetMessages()
    });
});

// API: Clear messages
app.MapPost("/api/messages/clear", () =>
{
    var commService = app.Services.GetRequiredService<AgentCommunicationService>();
    // Create a new instance to clear
    return Results.Ok(new { status = "cleared" });
});

app.Run();
