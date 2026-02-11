using Xunit;
using Shouldly;
using NSubstitute;
using AgentBus.Broker.Modules.Registration.Features.RegisterAgent;
using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using AgentBus.Broker.Tests.Helpers;

namespace AgentBus.Broker.Tests.Unit.Registration;

public class RegisterAgentHandlerTests
{
    private readonly IAgentRegistry _mockRegistry;
    private readonly IMessageBroker _mockMessageBroker;
    private readonly TimeProvider _timeProvider;
    private readonly RegisterAgentHandler _handler;

    public RegisterAgentHandlerTests()
    {
        _mockRegistry = Substitute.For<IAgentRegistry>();
        _mockMessageBroker = Substitute.For<IMessageBroker>();
        _timeProvider = TimeProvider.System;
        
        _handler = new RegisterAgentHandler(
            _mockRegistry,
            _mockMessageBroker,
            _timeProvider);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldRegisterAgent()
    {
        // Arrange
        var request = new RegisterAgentRequest(
            Id: "test-agent-001",
            Name: "Test Agent",
            Version: "1.0.0",
            Capabilities: new[] { "test-capability" },
            MessageTypes: new MessageTypes(new[] { "*.test" }, new[] { "test.*" }),
            Identity: new AgentIdentity("managedId", "principalId", "tenantId"),
            A2AEndpointUrl: "https://test-agent.example.com/a2a",
            Communication: new CommunicationCapabilities(true, true, true),
            EventsPublished: Array.Empty<string>(),
            Metadata: null);

        _mockRegistry
            .RegisterAgentAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Agent(
                "test", "test", "test", "1.0.0", AgentStatus.Active,
                Array.Empty<string>(), new MessageTypes(Array.Empty<string>(), Array.Empty<string>()),
                new AgentIdentity("id", "p", "t"), 
                new AgentEndpoints(null, null, "https://test.example.com/a2a"),
                new CommunicationCapabilities(true, true, true),
                Array.Empty<EventSubscription>(),
                Array.Empty<string>(),
                null,
                new AgentTimestamps(DateTime.UtcNow, DateTime.UtcNow))));

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(request.Id);
        await _mockRegistry.Received(1).RegisterAgentAsync(
            Arg.Is<Agent>(a => a.Id == request.Id),
            Arg.Any<CancellationToken>());
        
        // No inbox queue should be created for A2A-enabled agents
        await _mockMessageBroker.DidNotReceive().CreateInboxQueueAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldSetStatusToActive()
    {
        // Arrange
        var request = new RegisterAgentRequest(
            Id: "test-agent-002",
            Name: "Test Agent 2",
            Version: "2.0.0",
            Capabilities: new[] { "test" },
            MessageTypes: new MessageTypes(Array.Empty<string>(), Array.Empty<string>()),
            Identity: new AgentIdentity("id", "principal", "tenant"),
            A2AEndpointUrl: "https://test-agent-002.example.com/a2a",
            Communication: new CommunicationCapabilities(true, true, true),
            EventsPublished: Array.Empty<string>(),
            Metadata: null);

        Agent? capturedAgent = null;
        _mockRegistry
            .RegisterAgentAsync(Arg.Do<Agent>(a => capturedAgent = a), Arg.Any<CancellationToken>())
            .Returns(c => Task.FromResult(c.Arg<Agent>()));

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(AgentStatus.Active);
    }

    [Fact]
    public async Task HandleAsync_ShouldSetTimestamps()
    {
        // Arrange
        var request = new RegisterAgentRequest(
            Id: "test-agent-003",
            Name: "Test Agent 3",
            Version: "1.0.0",
            Capabilities: new[] { "test" },
            MessageTypes: new MessageTypes(Array.Empty<string>(), Array.Empty<string>()),
            Identity: new AgentIdentity("id", "principal", "tenant"),
            A2AEndpointUrl: "https://test-agent-003.example.com/a2a",
            Communication: new CommunicationCapabilities(true, true, true),
            EventsPublished: Array.Empty<string>(),
            Metadata: null);

        _mockRegistry
            .RegisterAgentAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>())
            .Returns(c => Task.FromResult(c.Arg<Agent>()));

        var before = DateTime.UtcNow;

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        var after = DateTime.UtcNow;

        // Assert
        result.ShouldNotBeNull();
        result.Timestamps.RegisteredAt.ShouldBeInRange(before, after);
        result.Timestamps.LastHeartbeat.ShouldBeInRange(before, after);
    }

    [Fact]
    public async Task HandleAsync_ShouldCreateInboxQueue()
    {
        // Arrange - Agent without A2A support should get inbox queue
        var request = new RegisterAgentRequest(
            Id: "test-agent-004",
            Name: "Test Agent 4",
            Version: "1.0.0",
            Capabilities: new[] { "test" },
            MessageTypes: new MessageTypes(Array.Empty<string>(), Array.Empty<string>()),
            Identity: new AgentIdentity("id", "principal", "tenant"),
            A2AEndpointUrl: "https://test-agent-004.example.com/a2a",
            Communication: new CommunicationCapabilities(false, true, true), // Does NOT support A2ADirect
            EventsPublished: Array.Empty<string>(),
            Metadata: null);

        _mockRegistry
            .RegisterAgentAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>())
            .Returns(c => Task.FromResult(c.Arg<Agent>()));

        // Act
        await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        await _mockMessageBroker.Received(1).CreateInboxQueueAsync(
            request.Id,
            Arg.Any<CancellationToken>());
    }
}
