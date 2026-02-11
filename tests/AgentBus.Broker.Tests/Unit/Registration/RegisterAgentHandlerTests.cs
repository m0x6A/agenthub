using AgentBus.Broker.Modules.Registration.Features.RegisterAgent;
using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AgentBus.Broker.Tests.Unit.Registration;

[Trait("Category", "Registration")]
public sealed class RegisterAgentHandlerTests
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IMessageBroker _messageBroker;
    private readonly RegisterAgentHandler _handler;
    private readonly TimeProvider _timeProvider;

    public RegisterAgentHandlerTests()
    {
        _agentRegistry = Substitute.For<IAgentRegistry>();
        _messageBroker = Substitute.For<IMessageBroker>();
        _timeProvider = TimeProvider.System;
        _handler = new RegisterAgentHandler(_agentRegistry, _messageBroker, _timeProvider);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsCreatedAgent()
    {
        // Arrange
        var request = new RegisterAgentRequest(
            Id: "test-agent-123",
            Name: "Test Agent",
            Version: "1.0.0",
            Capabilities: new[] { "test-capability" },
            MessageTypes: new MessageTypes(
                Accepts: new[] { "test.*" },
                Emits: new[] { "test.response" }
            ),
            Identity: new AgentIdentity(
                ManagedIdentityId: "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test",
                PrincipalId: "principal-123",
                TenantId: "tenant-123"
            ),
            Metadata: null
        );

        _agentRegistry.RegisterAgentAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _messageBroker.CreateInboxQueueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("test-agent-123");
        result.Name.ShouldBe("Test Agent");
        result.Version.ShouldBe("1.0.0");
        result.Status.ShouldBe(AgentStatus.Active);
        result.Capabilities.ShouldBe(request.Capabilities);
        result.Endpoints.InboxQueueName.ShouldBe("agent-test-agent-123-inbox");
        
        await _agentRegistry.Received(1).RegisterAgentAsync(
            Arg.Is<Agent>(a => a.Id == "test-agent-123"), 
            Arg.Any<CancellationToken>());
        
        await _messageBroker.Received(1).CreateInboxQueueAsync(
            "agent-test-agent-123-inbox", 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_SetsTimestamps()
    {
        // Arrange
        var request = new RegisterAgentRequest(
            Id: "test-agent-456",
            Name: "Test Agent",
            Version: "1.0.0",
            Capabilities: new[] { "test-capability" },
            MessageTypes: new MessageTypes(
                Accepts: new[] { "test.*" },
                Emits: new[] { "test.response" }
            ),
            Identity: new AgentIdentity(
                ManagedIdentityId: "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test",
                PrincipalId: "principal-456",
                TenantId: "tenant-456"
            ),
            Metadata: null
        );

        var beforeExecution = _timeProvider.GetUtcNow().UtcDateTime;

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        var afterExecution = _timeProvider.GetUtcNow().UtcDateTime;

        // Assert
        result.Timestamps.RegisteredAt.ShouldBeGreaterThanOrEqualTo(beforeExecution);
        result.Timestamps.RegisteredAt.ShouldBeLessThanOrEqualTo(afterExecution);
        result.Timestamps.LastHeartbeat.ShouldBe(result.Timestamps.RegisteredAt);
    }

    [Fact]
    public async Task Handle_WithMetadata_IncludesMetadataInAgent()
    {
        // Arrange
        var metadata = new AgentMetadata(
            Owner: "[email protected]",
            Environment: "dev",
            Tags: new[] { "test", "integration" }
        );

        var request = new RegisterAgentRequest(
            Id: "test-agent-789",
            Name: "Test Agent",
            Version: "1.0.0",
            Capabilities: new[] { "test-capability" },
            MessageTypes: new MessageTypes(
                Accepts: new[] { "test.*" },
                Emits: new[] { "test.response" }
            ),
            Identity: new AgentIdentity(
                ManagedIdentityId: "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test",
                PrincipalId: "principal-789",
                TenantId: "tenant-789"
            ),
            Metadata: metadata
        );

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.Metadata.ShouldNotBeNull();
        result.Metadata.Owner.ShouldBe("[email protected]");
        result.Metadata.Environment.ShouldBe("dev");
        result.Metadata.Tags.ShouldBe(new[] { "test", "integration" });
    }

    [Fact]
    public async Task Handle_GeneratesCorrectInboxQueueName()
    {
        // Arrange
        var request = new RegisterAgentRequest(
            Id: "logistics-coordinator-v1-abc123",
            Name: "Logistics Coordinator",
            Version: "1.0.0",
            Capabilities: new[] { "schedule-drone" },
            MessageTypes: new MessageTypes(
                Accepts: new[] { "command.logistics.*" },
                Emits: new[] { "event.logistics.route-assigned" }
            ),
            Identity: new AgentIdentity(
                ManagedIdentityId: "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test",
                PrincipalId: "principal-abc123",
                TenantId: "tenant-abc123"
            ),
            Metadata: null
        );

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.Endpoints.InboxQueueName.ShouldBe("agent-logistics-coordinator-v1-abc123-inbox");
        
        await _messageBroker.Received(1).CreateInboxQueueAsync(
            "agent-logistics-coordinator-v1-abc123-inbox", 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RegistryFailure_ThrowsException()
    {
        // Arrange
        var request = new RegisterAgentRequest(
            Id: "test-agent-error",
            Name: "Test Agent",
            Version: "1.0.0",
            Capabilities: new[] { "test-capability" },
            MessageTypes: new MessageTypes(
                Accepts: new[] { "test.*" },
                Emits: new[] { "test.response" }
            ),
            Identity: new AgentIdentity(
                ManagedIdentityId: "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test",
                PrincipalId: "principal-error",
                TenantId: "tenant-error"
            ),
            Metadata: null
        );

        _agentRegistry.RegisterAgentAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>())
        _agentRegistry.RegisterAgentAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Registry unavailable"));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _handler.HandleAsync(request, CancellationToken.None));
    }

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _handler.HandleAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_QueueCreationFailure_ThrowsException()
    {
        // Arrange
        var request = new RegisterAgentRequest(
            Id: "test-agent-queue-error",
            Name: "Test Agent",
            Version: "1.0.0",
            Capabilities: new[] { "test-capability" },
            MessageTypes: new MessageTypes(
                Accepts: new[] { "test.*" },
                Emits: new[] { "test.response" }
            ),
            Identity: new AgentIdentity(
                ManagedIdentityId: "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test",
        _messageBroker.CreateInboxQueueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Queue creation failed"));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _handler.HandleAsync(request, CancellationToken.None));
    }
            Metadata: null
        );

        _messageBroker.CreateInboxQueueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            ;
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _handler.HandleAsync(request, CancellationToken.None));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _handler.HandleAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithHealthCheckUrl_IncludesInEndpoints()
    {
        // Arrange
        var request = new RegisterAgentRequest(
            Id: "test-agent-health",
            Name: "Test Agent",
            Version: "1.0.0",
            Capabilities: new[] { "test-capability" },
            MessageTypes: new MessageTypes(
                Accepts: new[] { "test.*" },
                Emits: new[] { "test.response" }
            ),
            Identity: new AgentIdentity(
                ManagedIdentityId: "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test",
                PrincipalId: "principal-health",
                TenantId: "tenant-health"
            ),
            Metadata: null,
            HealthCheckUrl: "https://test-agent.example.com/health"
        );

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.Endpoints.HealthCheckUrl.ShouldBe("https://test-agent.example.com/health");
    }

    [Fact]
    public async Task Handle_CancellationToken_PropagatedToRegistry()
    {
        // Arrange
        var request = new RegisterAgentRequest(
            Id: "test-agent-cancel",
            Name: "Test Agent",
            Version: "1.0.0",
            Capabilities: new[] { "test-capability" },
            MessageTypes: new MessageTypes(
                Accepts: new[] { "test.*" },
                Emits: new[] { "test.response" }
            ),
            Identity: new AgentIdentity(
                ManagedIdentityId: "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test",
                PrincipalId: "principal-cancel",
                TenantId: "tenant-cancel"
            ),
            Metadata: null
        );

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _agentRegistry.RegisterAgentAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var token = callInfo.Arg<CancellationToken>();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _handler.HandleAsync(request, cts.Token));
    }
}
