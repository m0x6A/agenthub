using Xunit;
using Shouldly;
using NSubstitute;
using AgentBus.Broker.Modules.Eventing.Features.PublishEvent;
using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using AgentBus.Broker.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace AgentBus.Broker.Tests.Unit.Eventing;

public class PublishEventHandlerTests
{
    private readonly IEventBroker _mockEventBroker;
    private readonly ILogger<PublishEventHandler> _mockLogger;
    private readonly PublishEventHandler _handler;

    public PublishEventHandlerTests()
    {
        _mockEventBroker = Substitute.For<IEventBroker>();
        _mockLogger = Substitute.For<ILogger<PublishEventHandler>>();
        _handler = new PublishEventHandler(_mockEventBroker, _mockLogger);
    }

    [Fact]
    public async Task HandleAsync_WithValidEvent_ShouldPublishSuccessfully()
    {
        // Arrange
        var sourceAgentId = "publisher-agent-001";
        var request = new PublishEventRequest(
            EventType: "events.test.published",
            DataVersion: "1.0.0",
            Data: new { testData = "value" },
            Headers: null);

        _mockEventBroker
            .PublishEventAsync(Arg.Any<EventEnvelope>(), Arg.Any<CancellationToken>())
            .Returns("evt-123");

        // Act
        var result = await _handler.HandleAsync(request, sourceAgentId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.EventId.ShouldBe("evt-123");
        await _mockEventBroker.Received(1).PublishEventAsync(
            Arg.Is<EventEnvelope>(e => 
                e.Source == sourceAgentId &&
                e.EventType == request.EventType &&
                e.DataVersion == request.DataVersion),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldSetTimestamp()
    {
        // Arrange
        var request = new PublishEventRequest(
            EventType: "events.test.event",
            DataVersion: "1.0.0",
            Data: new { test = "data" });

        EventEnvelope? capturedEnvelope = null;
        _mockEventBroker
            .PublishEventAsync(Arg.Do<EventEnvelope>(e => capturedEnvelope = e), Arg.Any<CancellationToken>())
            .Returns("evt-id");

        var before = DateTime.UtcNow;

        // Act
        await _handler.HandleAsync(request, "source-agent", CancellationToken.None);

        var after = DateTime.UtcNow;

        // Assert
        capturedEnvelope.ShouldNotBeNull();
        capturedEnvelope.Timestamp.ShouldBeInRange(before, after);
    }

    [Fact]
    public async Task HandleAsync_WithHeaders_ShouldIncludeInEnvelope()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            { "x-trace-id", "trace-123" },
            { "x-correlation-id", "corr-456" }
        };

        var request = new PublishEventRequest(
            EventType: "events.test.event",
            DataVersion: "1.0.0",
            Data: new { test = "data" },
            Headers: headers);

        EventEnvelope? capturedEnvelope = null;
        _mockEventBroker
            .PublishEventAsync(Arg.Do<EventEnvelope>(e => capturedEnvelope = e), Arg.Any<CancellationToken>())
            .Returns("evt-id");

        // Act
        await _handler.HandleAsync(request, "source-agent", CancellationToken.None);

        // Assert
        capturedEnvelope.ShouldNotBeNull();
        capturedEnvelope.Headers.ShouldNotBeNull();
        capturedEnvelope.Headers.ShouldContainKeyAndValue("x-trace-id", "trace-123");
        capturedEnvelope.Headers.ShouldContainKeyAndValue("x-correlation-id", "corr-456");
    }

    [Fact]
    public async Task HandleAsync_ShouldGenerateUniqueEventId()
    {
        // Arrange
        var request = new PublishEventRequest(
            EventType: "events.test.event",
            DataVersion: "1.0.0",
            Data: new { test = "data" });

        var eventIds = new List<string>();
        _mockEventBroker
            .PublishEventAsync(Arg.Any<EventEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(c => 
            {
                var envelope = c.Arg<EventEnvelope>();
                eventIds.Add(envelope.EventId);
                return envelope.EventId;
            });

        // Act
        await _handler.HandleAsync(request, "source-1", CancellationToken.None);
        await _handler.HandleAsync(request, "source-2", CancellationToken.None);

        // Assert
        eventIds.Count.ShouldBe(2);
        eventIds[0].ShouldNotBe(eventIds[1]);
    }
}
