using Xunit;
using Shouldly;
using NSubstitute;
using AgentBus.Broker.Modules.Eventing.Features.SubscribeToEvent;
using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using AgentBus.Broker.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace AgentBus.Broker.Tests.Unit.Eventing;

public class SubscribeToEventHandlerTests
{
    private readonly IEventBroker _mockEventBroker;
    private readonly ILogger<SubscribeToEventHandler> _mockLogger;
    private readonly SubscribeToEventHandler _handler;

    public SubscribeToEventHandlerTests()
    {
        _mockEventBroker = Substitute.For<IEventBroker>();
        _mockLogger = Substitute.For<ILogger<SubscribeToEventHandler>>();
        _handler = new SubscribeToEventHandler(_mockEventBroker, _mockLogger);
    }

    [Fact]
    public async Task HandleAsync_ShouldSubscribeToEventType()
    {
        // Arrange
        var agentId = "subscriber-agent-001";
        var eventType = "events.test.published";
        var request = new SubscribeToEventRequest(EventType: eventType, Filters: null);

        var expectedSubscription = TestDataBuilder.CreateTestSubscription(agentId, eventType);

        _mockEventBroker
            .SubscribeAsync(agentId, eventType, null, Arg.Any<CancellationToken>())
            .Returns(expectedSubscription);

        // Act
        var result = await _handler.HandleAsync(request, agentId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Subscription.AgentId.ShouldBe(agentId);
        result.Subscription.EventType.ShouldBe(eventType);
    }

    [Fact]
    public async Task HandleAsync_WithFilters_ShouldPassFiltersToEventBroker()
    {
        // Arrange
        var agentId = "subscriber-agent-002";
        var eventType = "events.test.filtered";
        var filters = new[] { "severity='high'", "region='west'" };
        var request = new SubscribeToEventRequest(EventType: eventType, Filters: filters);

        var expectedSubscription = TestDataBuilder.CreateTestSubscription(agentId, eventType);
        expectedSubscription = expectedSubscription with { Filters = filters };

        _mockEventBroker
            .SubscribeAsync(agentId, eventType, filters, Arg.Any<CancellationToken>())
            .Returns(expectedSubscription);

        // Act
        var result = await _handler.HandleAsync(request, agentId, CancellationToken.None);

        // Assert
        result.Subscription.Filters.ShouldBe(filters);
        await _mockEventBroker.Received(1).SubscribeAsync(
            agentId,
            eventType,
            filters,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnSubscriptionWithId()
    {
        // Arrange
        var request = new SubscribeToEventRequest(
            EventType: "events.test.event",
            Filters: null);

        var subscription = TestDataBuilder.CreateTestSubscription();

        _mockEventBroker
            .SubscribeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]?>(), Arg.Any<CancellationToken>())
            .Returns(subscription);

        // Act
        var result = await _handler.HandleAsync(request, "agent-id", CancellationToken.None);

        // Assert
        result.Subscription.Id.ShouldNotBeNullOrEmpty();
    }
}
