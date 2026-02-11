using Xunit;
using Shouldly;
using NSubstitute;
using AgentBus.Broker.Modules.Messaging.Features.SendMessage;
using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using AgentBus.Broker.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace AgentBus.Broker.Tests.Unit.Messaging;

public class SendMessageHandlerTests
{
    private readonly IAgentRegistry _mockRegistry;
    private readonly IMessageBroker _mockMessageBroker;
    private readonly ILogger<SendMessageHandler> _mockLogger;
    private readonly SendMessageHandler _handler;

    public SendMessageHandlerTests()
    {
        _mockRegistry = Substitute.For<IAgentRegistry>();
        _mockMessageBroker = Substitute.For<IMessageBroker>();
        _mockLogger = Substitute.For<ILogger<SendMessageHandler>>();
        
        _handler = new SendMessageHandler(
            _mockRegistry,
            _mockMessageBroker,
            _mockLogger);
    }

    [Fact]
    public async Task HandleAsync_WithValidRecipient_ShouldSendMessage()
    {
        // Arrange
        var fromAgentId = "sender-agent-001";
        var toAgent = TestDataBuilder.CreateTestAgent(id: "receiver-agent-002");
        
        var request = new SendMessageRequest(
            To: toAgent.Id,
            MessageType: "test.message",
            Payload: new { test = "data" },
            CorrelationId: null,
            ConversationId: null,
            TtlSeconds: null,
            Headers: null);

        _mockRegistry
            .GetAgentByIdAsync(toAgent.Id, Arg.Any<CancellationToken>())
            .Returns(toAgent);

        _mockMessageBroker
            .SendMessageAsync(Arg.Any<MessageEnvelope>(), Arg.Any<CancellationToken>())
            .Returns("msg-123");

        // Act
        var result = await _handler.HandleAsync(request, fromAgentId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.MessageId.ShouldBe("msg-123");
        await _mockMessageBroker.Received(1).SendMessageAsync(
            Arg.Is<MessageEnvelope>(m => 
                m.From == fromAgentId && 
                m.To == toAgent.Id &&
                m.MessageType == request.MessageType),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentRecipient_ShouldThrowException()
    {
        // Arrange
        var request = new SendMessageRequest(
            To: "non-existent-agent",
            MessageType: "test.message",
            Payload: new { test = "data" });

        _mockRegistry
            .GetAgentByIdAsync("non-existent-agent", Arg.Any<CancellationToken>())
            .Returns((Agent?)null);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await _handler.HandleAsync(request, "sender", CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ShouldSetDefaultTtl()
    {
        // Arrange
        var toAgent = TestDataBuilder.CreateTestAgent();
        var request = new SendMessageRequest(
            To: toAgent.Id,
            MessageType: "test.message",
            Payload: new { test = "data" },
            TtlSeconds: null);

        _mockRegistry
            .GetAgentByIdAsync(toAgent.Id, Arg.Any<CancellationToken>())
            .Returns(toAgent);

        MessageEnvelope? capturedEnvelope = null;
        _mockMessageBroker
            .SendMessageAsync(Arg.Do<MessageEnvelope>(e => capturedEnvelope = e), Arg.Any<CancellationToken>())
            .Returns("msg-id");

        // Act
        await _handler.HandleAsync(request, "sender", CancellationToken.None);

        // Assert
        capturedEnvelope.ShouldNotBeNull();
        capturedEnvelope.Ttl.ShouldBe(TimeSpan.FromHours(24));
    }

    [Fact]
    public async Task HandleAsync_WithCorrelationId_ShouldIncludeInEnvelope()
    {
        // Arrange
        var toAgent = TestDataBuilder.CreateTestAgent();
        var correlationId = "corr-123";
        var request = new SendMessageRequest(
            To: toAgent.Id,
            MessageType: "test.message",
            Payload: new { test = "data" },
            CorrelationId: correlationId);

        _mockRegistry.GetAgentByIdAsync(toAgent.Id, Arg.Any<CancellationToken>()).Returns(toAgent);

        MessageEnvelope? capturedEnvelope = null;
        _mockMessageBroker
            .SendMessageAsync(Arg.Do<MessageEnvelope>(e => capturedEnvelope = e), Arg.Any<CancellationToken>())
            .Returns("msg-id");

        // Act
        await _handler.HandleAsync(request, "sender", CancellationToken.None);

        // Assert
        capturedEnvelope.ShouldNotBeNull();
        capturedEnvelope.CorrelationId.ShouldBe(correlationId);
    }
}
