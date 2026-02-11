using AgentBus.Broker.Modules.Registration.Features.UpdateHeartbeat;
using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AgentBus.Broker.Tests.Unit.Registration;

[Trait("Category", "Registration")]
public sealed class UpdateHeartbeatHandlerTests
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly TimeProvider _timeProvider;
    private readonly UpdateHeartbeatHandler _handler;

    public UpdateHeartbeatHandlerTests()
    {
        _agentRegistry = Substitute.For<IAgentRegistry>();
        _timeProvider = TimeProvider.System;
        _handler = new UpdateHeartbeatHandler(_agentRegistry, _timeProvider);
    }

    [Fact]
    public async Task Handle_ExistingAgent_UpdatesHeartbeat()
    {
        // Arrange
        var agentId = "test-agent-123";
        var existingAgent = CreateAgent(agentId, AgentStatus.Active);

        _agentRegistry.GetAgentByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(existingAgent);

        _agentRegistry.UpdateHeartbeatAsync(agentId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new UpdateHeartbeatRequest(AgentId: agentId);

        // Act
        await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        await _agentRegistry.Received(1).UpdateHeartbeatAsync(
            agentId,
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InactiveAgent_ReactivatesAgent()
    {
        // Arrange
        var agentId = "test-agent-456";
        var existingAgent = CreateAgent(agentId, AgentStatus.Inactive);

        _agentRegistry.GetAgentByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(existingAgent);

        _agentRegistry.UpdateHeartbeatAsync(agentId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _agentRegistry.UpdateStatusAsync(agentId, AgentStatus.Active, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new UpdateHeartbeatRequest(AgentId: agentId);

        // Act
        await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        await _agentRegistry.Received(1).UpdateStatusAsync(
            agentId,
            AgentStatus.Active,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonExistentAgent_ThrowsException()
    {
        // Arrange
        var agentId = "non-existent-agent";

        _agentRegistry.GetAgentByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns((Agent?)null);

        var request = new UpdateHeartbeatRequest(AgentId: agentId);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _handler.HandleAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UsesCurrentTime()
    {
        // Arrange
        var agentId = "test-agent-789";
        var existingAgent = CreateAgent(agentId, AgentStatus.Active);

        _agentRegistry.GetAgentByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(existingAgent);

        var beforeExecution = _timeProvider.GetUtcNow().UtcDateTime;

        var request = new UpdateHeartbeatRequest(AgentId: agentId);

        // Act
        await _handler.HandleAsync(request, CancellationToken.None);

        var afterExecution = _timeProvider.GetUtcNow().UtcDateTime;

        // Assert
        await _agentRegistry.Received(1).UpdateHeartbeatAsync(
            agentId,
            Arg.Is<DateTime>(dt => dt >= beforeExecution && dt <= afterExecution),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancellationToken_PropagatedToRegistry()
    {
        // Arrange
        var agentId = "test-agent-cancel";
        var existingAgent = CreateAgent(agentId, AgentStatus.Active);

        _agentRegistry.GetAgentByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(existingAgent);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _agentRegistry.UpdateHeartbeatAsync(agentId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var token = callInfo.Arg<CancellationToken>();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        var request = new UpdateHeartbeatRequest(AgentId: agentId);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _handler.HandleAsync(request, cts.Token));
    }

    private static Agent CreateAgent(string id, AgentStatus status)
    {
        return new Agent(
            Id: id,
            PartitionKey: id,
            Name: "Test Agent",
            Version: "1.0.0",
            Status: status,
            Capabilities: new[] { "test-capability" },
            MessageTypes: new MessageTypes(new[] { "*" }, new[] { "*" }),
            Identity: new AgentIdentity($"/subscriptions/test/mi/{id}", $"principal-{id}", "tenant-123"),
            Endpoints: new AgentEndpoints($"agent-{id}-inbox", null),
            Metadata: null,
            Timestamps: new AgentTimestamps(
                DateTime.UtcNow.AddHours(-1),
                DateTime.UtcNow.AddMinutes(-10))
        );
    }
}
