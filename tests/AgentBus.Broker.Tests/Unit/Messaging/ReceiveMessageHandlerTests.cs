using Xunit;
using Shouldly;
using NSubstitute;
using AgentBus.Broker.Modules.Messaging.Features.ReceiveMessage;
using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using AgentBus.Broker.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace AgentBus.Broker.Tests.Unit.Messaging;

public class ReceiveMessageHandlerTests
{
    private readonly IMessageBroker _mockMessageBroker;
    private readonly ILogger<ReceiveMessageHandler> _mockLogger;
    private readonly ReceiveMessageHandler _handler;

    public ReceiveMessageHandlerTests()
    {
        _mockMessageBroker = Substitute.For<IMessageBroker>();
        _mockLogger = Substitute.For<ILogger<ReceiveMessageHandler>>();
        _handler = new ReceiveMessageHandler(_mockMessageBroker, _mockLogger);
    }

    [Fact]
    public async Task HandleAsync_WhenMessagesAvailable_ShouldReturnMessage()
    {
        // Arrange
        var agentId = "receiver-agent-001";
        var timeout = 30;
        var expectedMessage = TestDataBuilder.CreateTestMessage(to: agentId);

        _mockMessageBroker
            .ReceiveMessageAsync(agentId, timeout, Arg.Any<CancellationToken>())
            .Returns(expectedMessage);

        // Act
        var result = await _handler.HandleAsync(agentId, timeout, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldNotBeNull();
        result.Message.To.ShouldBe(agentId);
    }

    [Fact]
    public async Task HandleAsync_WhenNoMessages_ShouldReturnNull()
    {
        // Arrange
        var agentId = "receiver-agent-002";
        var timeout = 5;

        _mockMessageBroker
            .ReceiveMessageAsync(agentId, timeout, Arg.Any<CancellationToken>())
            .Returns((MessageEnvelope?)null);

        // Act
        var result = await _handler.HandleAsync(agentId, timeout, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_ShouldPassTimeoutToMessageBroker()
    {
        // Arrange
        var agentId = "receiver-agent-003";
        var timeout = 45;

        _mockMessageBroker
            .ReceiveMessageAsync(agentId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((MessageEnvelope?)null);

        // Act
        await _handler.HandleAsync(agentId, timeout, CancellationToken.None);

        // Assert
        await _mockMessageBroker.Received(1).ReceiveMessageAsync(
            agentId,
            timeout,
            Arg.Any<CancellationToken>());
    }
}
