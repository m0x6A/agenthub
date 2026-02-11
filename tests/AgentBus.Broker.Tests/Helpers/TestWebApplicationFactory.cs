using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AgentBus.Broker.SharedKernel.Interfaces;
using A2A;
using NSubstitute;

namespace AgentBus.Broker.Tests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public IAgentRegistry MockAgentRegistry { get; private set; } = null!;
    public IMessageBroker MockMessageBroker { get; private set; } = null!;
    public IEventBroker MockEventBroker { get; private set; } = null!;
    public ITaskStore MockTaskStore { get; private set; } = null!;
    public ITaskManager MockTaskManager { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove real services
            services.RemoveAll<IAgentRegistry>();
            services.RemoveAll<IMessageBroker>();
            services.RemoveAll<IEventBroker>();
            services.RemoveAll<ITaskStore>();
            services.RemoveAll<ITaskManager>();
            services.RemoveAll(typeof(Microsoft.Azure.Cosmos.CosmosClient));
            services.RemoveAll(typeof(Azure.Messaging.ServiceBus.ServiceBusClient));

            // Add mock services
            MockAgentRegistry = Substitute.For<IAgentRegistry>();
            MockMessageBroker = Substitute.For<IMessageBroker>();
            MockEventBroker = Substitute.For<IEventBroker>();
            MockTaskStore = Substitute.For<ITaskStore>();
            MockTaskManager = Substitute.For<ITaskManager>();

            services.AddSingleton(MockAgentRegistry);
            services.AddSingleton(MockMessageBroker);
            services.AddSingleton(MockEventBroker);
            services.AddSingleton(MockTaskStore);
            services.AddSingleton(MockTaskManager);
        });

        builder.UseEnvironment("Testing");
    }

    public void ResetMocks()
    {
        MockAgentRegistry.ClearReceivedCalls();
        MockMessageBroker.ClearReceivedCalls();
        MockEventBroker.ClearReceivedCalls();
        MockTaskStore.ClearReceivedCalls();
        MockTaskManager.ClearReceivedCalls();
    }
}
