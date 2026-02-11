var builder = DistributedApplication.CreateBuilder(args);

// Azure Service Bus for agent communication (will provision in Azure or use local emulator)
var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator(); // Use local emulator for development

// AgentBus Broker - Core message broker for all agents
var broker = builder.AddProject<Projects.AgentBus_Broker>("agentbus-broker")
    .WithReference(serviceBus)
    .WithHttpEndpoint(port: 5000, name: "http");

// Autonomous Agent 1: Customer Experience (HTTP transport)
var customerAgent = builder.AddProject<Projects.AgentBus_Examples_CustomerExperience>("customer-experience-agent")
    .WithReference(broker)
    .WithEnvironment("AGENTBUS_URL", broker.GetEndpoint("http"));

// Autonomous Agent 2: Operations & Inventory (Service Bus transport)
var operationsAgent = builder.AddProject<Projects.AgentBus_Examples_OperationsInventory>("operations-inventory-agent")
    .WithReference(broker)
    .WithReference(serviceBus)
    .WithEnvironment("AGENTBUS_URL", broker.GetEndpoint("http"));

// Autonomous Agent 3: Financial Authorization (HTTP transport)
var financialAgent = builder.AddProject<Projects.AgentBus_Examples_FinancialAuth>("financial-authorization-agent")
    .WithReference(broker)
    .WithEnvironment("AGENTBUS_URL", broker.GetEndpoint("http"));

// Autonomous Agent 4: Internal Communications (Service Bus transport, Global observer)
var commsAgent = builder.AddProject<Projects.AgentBus_Examples_InternalComms>("internal-comms-agent")
    .WithReference(broker)
    .WithReference(serviceBus)
    .WithEnvironment("AGENTBUS_URL", broker.GetEndpoint("http"));

builder.Build().Run();
