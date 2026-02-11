var builder = DistributedApplication.CreateBuilder(args);

Console.WriteLine("🚀 Starting Aspire AppHost...");

// Azure OpenAI for autonomous agent LLM reasoning
// Uses connection string from user secrets for local development
var openai = builder.AddConnectionString("openai");
Console.WriteLine("✅ Added Azure OpenAI connection");

// Azure Cosmos DB for agent registry and subscription storage
Console.WriteLine("📦 Adding Cosmos DB...");
var cosmosDb = builder.AddAzureCosmosDB("cosmosdb")
    .RunAsEmulator();  // Use local emulator for development
Console.WriteLine("✅ Added Cosmos DB emulator");

// Azure Service Bus for agent communication
Console.WriteLine("📦 Adding Service Bus...");
var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator(); // Use local emulator for development
Console.WriteLine("✅ Added Service Bus emulator");

// AgentBus Broker - Core message broker for all agents
Console.WriteLine("📦 Adding AgentBus.Broker project...");
var broker = builder.AddProject<Projects.AgentBus_Broker>("agentbus-broker")
    .WithReference(cosmosDb)
    .WithReference(serviceBus)
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithEnvironment("CosmosDb:DatabaseName", "agentbus");
Console.WriteLine("✅ Added broker with references");

Console.WriteLine("📦 Adding autonomous agents...");
// Autonomous Agent 1: Customer Experience (HTTP transport + LLM)
var customerAgent = builder.AddProject<Projects.AgentBus_Examples_CustomerExperience>("customer-experience-agent")
    .WithReference(broker)
    .WithReference(openai)  // Provides Azure OpenAI connection
    .WithEnvironment("AGENTBUS_URL", broker.GetEndpoint("http"));
Console.WriteLine("✅ Added Customer Experience Agent");

// Autonomous Agent 2: Operations & Inventory (Service Bus transport + LLM)
var operationsAgent = builder.AddProject<Projects.AgentBus_Examples_OperationsInventory>("operations-inventory-agent")
    .WithReference(broker)
    .WithReference(serviceBus)
    .WithReference(openai)
    .WithEnvironment("AGENTBUS_URL", broker.GetEndpoint("http"));
Console.WriteLine("✅ Added Operations & Inventory Agent");

// Autonomous Agent 3: Financial Authorization (HTTP transport + LLM)
var financialAgent = builder.AddProject<Projects.AgentBus_Examples_FinancialAuth>("financial-authorization-agent")
    .WithReference(broker)
    .WithReference(openai)
    .WithEnvironment("AGENTBUS_URL", broker.GetEndpoint("http"));
Console.WriteLine("✅ Added Financial Authorization Agent");

// Autonomous Agent 4: Internal Communications (Service Bus transport + LLM, Global observer)
var commsAgent = builder.AddProject<Projects.AgentBus_Examples_InternalComms>("internal-comms-agent")
    .WithReference(broker)
    .WithReference(serviceBus)
    .WithReference(openai)
    .WithEnvironment("AGENTBUS_URL", broker.GetEndpoint("http"));
Console.WriteLine("✅ Added Internal Communications Agent");

// Logistics Coordination Agents - Two agents that collaborate
Console.WriteLine("📦 Adding Logistics Coordination Agents...");

// Shipping Agent - Initiates logistics requests
var shippingAgent = builder.AddProject<Projects.AgentBus_Examples_LogisticsCoordination>("shipping-agent")
    .WithReference(broker)
    .WithReference(openai)
    .WithEnvironment("AGENTBUS_URL", broker.GetEndpoint("http"))
    .WithEnvironment("AGENT_MODE", "ShippingAgent");
Console.WriteLine("✅ Added Shipping Agent");

// Warehouse Lead Agent - Responds to logistics coordination requests
var warehouseAgent = builder.AddProject<Projects.AgentBus_Examples_LogisticsCoordination>("warehouse-lead-agent")
    .WithReference(broker)
    .WithReference(openai)
    .WithEnvironment("AGENTBUS_URL", broker.GetEndpoint("http"))
    .WithEnvironment("AGENT_MODE", "WarehouseLead");
Console.WriteLine("✅ Added Warehouse Lead Agent");

// Web Dashboard - Interactive showcase of agent communication
Console.WriteLine("📦 Adding Logistics Coordination UI...");
var logisticsUI = builder.AddProject<Projects.AgentBus_Examples_LogisticsUI>("logistics-ui")
    .WithReference(broker)
    .WithEnvironment("AgentBusUrl", broker.GetEndpoint("http"))
    .WithHttpEndpoint(port: 5001, name: "http");
Console.WriteLine("✅ Added Logistics Coordination UI");

Console.WriteLine("🔨 Building Aspire application...");
var app = builder.Build();
Console.WriteLine("✨ Aspire AppHost started successfully!");

app.Run();
