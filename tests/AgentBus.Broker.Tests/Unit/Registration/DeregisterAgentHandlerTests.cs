using AgentBus.Broker.Modules.Registration.Features.DeregisterAgent;
using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AgentBus.Broker.Tests.Unit.Registration;

[Trait("Category", "Registration")]
public sealed class DeregisterAgentHandlerTests
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IMessageBroker _messageBroker;
    private readonly IEventBroker _eventBroker;
    private readonly DeregisterAgentHandler _handler;

    public DeregisterAgentHandlerTests()
    {
        _agentRegistry = Substitute.For<IAgentRegistry>();
        _messageBroker = Substitute.For<IMessageBroker>();
        _eventBroker = Substitute.For<IEventBroker>();
        _handler = new DeregisterAgentHandler(_agentRegistry, _messageBroker, _eventBroker);
    }

    [Fact]
    public async Task Handle_ExistingAgent_DeletesAgentAndQueue()
    {
        // Arrange
        var agentId = "test-agent-123";
        var agent = CreateAgent(agentId);

        _agentRegistry.GetAgentByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);

        _agentRegistry.DeleteAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _messageBroker.DeleteInboxQueueAsync(agent.Endpoints.InboxQueueName, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _eventBroker.DeleteAllSubscriptionsAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new DeregisterAgentRequest(AgentId: agentId);

        // Act
        await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        await _agentRegistry.Received(1).DeleteAgentAsync(agentId, Arg.Any<CancellationToken>("); return Task.CompletedTask; })
        await _messageBroker.Received(1).DeleteInboxQueueAsync(
            agent.Endpoints.InboxQueueName, 
            Arg.Any<CancellationToken>("); return Task.CompletedTask; })
        await _eventBroker.Received(1).DeleteAllSubscriptionsAsync(agentId, Arg.Any<CancellationToken>("); return Task.CompletedTask; })
    }

    [Fact]
    public async Task Handle_NonExistentAgent_ThrowsException()
    {
        // Arrange
        var agentId = "non-existent-agent";

        _agentRegistry.GetAgentByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns((Agent?)null);

        var request = new DeregisterAgentRequest(AgentId: agentId);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _handler.HandleAsync(request, CancellationToken.None"); return Task.CompletedTask; })

        await _agentRegistry.DidNotReceive().DeleteAgentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>("); return Task.CompletedTask; })
        await _messageBroker.DidNotReceive().DeleteInboxQueueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>("); return Task.CompletedTask; })
    }

    [Fact]
    public async Task Handle_RegistryDeletionFailure_DoesNotDeleteQueue()
    {
        // Arrange
        var agentId = "test-agent-error";
        var agent = CreateAgent(agentId);

        _agentRegistry.GetAgentByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);

        _agentRegistry.DeleteAgentAsync(agentId, Arg.Any<CancellationToken>())
            ;
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _handler.HandleAsync(request, CancellationToken.None"); return Task.CompletedTask; })

        var request = new DeregisterAgentRequest(AgentId: agentId);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _handler.HandleAsync(request, CancellationToken.None"); return Task.CompletedTask; })

        await _messageBroker.DidNotReceive().DeleteInboxQueueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>("); return Task.CompletedTask; })
    }

    [Fact]
    public async Task Handle_QueueDeletionFailure_ThrowsException()
    {
        // Arrange
        var agentId = "test-agent-queue-error";
        var agent = CreateAgent(agentId);

        _agentRegistry.GetAgentByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);

        _agentRegistry.DeleteAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _messageBroker.DeleteInboxQueueAsync(agent.Endpoints.InboxQueueName, Arg.Any<CancellationToken>())
            ;
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _handler.HandleAsync(request, CancellationToken.None"); return Task.CompletedTask; })

        var request = new DeregisterAgentRequest(AgentId: agentId);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _handler.HandleAsync(request, CancellationToken.None"); return Task.CompletedTask; })
    }

    [Fact]
    public async Task Handle_CancellationToken_PropagatedToRegistry()
    {
        // Arrange
        var agentId = "test-agent-cancel";
        var agent = CreateAgent(agentId);

        _agentRegistry.GetAgentByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _agentRegistry.DeleteAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var token = callInfo.Arg<CancellationToken>();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        var request = new DeregisterAgentRequest(AgentId: agentId);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _handler.HandleAsync(request, cts.Token"); return Task.CompletedTask; })
    }

    [Fact]
    public async Task Handle_DeletesSubscriptionsBeforeQueue()
    {
        // Arrange
        var agentId = "test-agent-order";
        var agent = CreateAgent(agentId);
        var callOrder = new List<string>();

        _agentRegistry.GetAgentByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);

        _agentRegistry.DeleteAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(callInfo => { callOrder.Add("deregister")"); return Task.CompletedTask; })

        _eventBroker.DeleteAllSubscriptionsAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(callOrder.Add("subscriptions")"); return Task.CompletedTask; })

        _messageBroker.DeleteInboxQueueAsync(agent.Endpoints.InboxQueueName, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(callOrder.Add("queue")"); return Task.CompletedTask; })

        var request = new DeregisterAgentRequest(AgentId: agentId);

        // Act
        await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        callOrder.ShouldBe(new[] { "subscriptions", "queue", "deregister" });
    }

    private static Agent CreateAgent(string id)
    {
        return new Agent(
            Id: id,
            PartitionKey: id,
            Name: "Test Agent",
            Version: "1.0.0",
            Status: AgentStatus.Active,
            Capabilities: new[] { "test-capability" },
            MessageTypes: new MessageTypes(new[] { "*" }, new[] { "*" }),
            Identity: new AgentIdentity($"/subscriptions/test/mi/{id}", $"principal-{id}", "tenant-123"),
            Endpoints: new AgentEndpoints($"agent-{id}-inbox", null),
            Metadata: null,
            Timestamps: new AgentTimestamps(DateTime.UtcNow, DateTime.UtcNow)
        );
    }
}
