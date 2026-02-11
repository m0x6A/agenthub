using Xunit;
using Shouldly;
using NSubstitute;
using AgentBus.Broker.Modules.Registration.Features.DiscoverAgents;
using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using AgentBus.Broker.Tests.Helpers;

namespace AgentBus.Broker.Tests.Unit.Registration;

public class DiscoverAgentsHandlerTests
{
    private readonly IAgentRegistry _mockRegistry;
    private readonly DiscoverAgentsHandler _handler;

    public DiscoverAgentsHandlerTests()
    {
        _mockRegistry = Substitute.For<IAgentRegistry>();
        _handler = new DiscoverAgentsHandler(_mockRegistry);
    }

    [Fact]
    public async Task HandleAsync_WithCapabilityFilter_ShouldFindMatchingAgents()
    {
        // Arrange
        var capability = "schedule-drone";
        var request = new DiscoverAgentsRequest(Capability: capability, Status: null);
        
        var expectedAgents = new[]
        {
            TestDataBuilder.CreateTestAgent(capabilities: new[] { capability }),
            TestDataBuilder.CreateTestAgent(capabilities: new[] { capability, "other" })
        };

        _mockRegistry
            .FindAgentsByCapabilityAsync(capability, Arg.Any<CancellationToken>())
            .Returns(expectedAgents);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Agents.Length.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task HandleAsync_WithStatusFilter_ShouldFindMatchingAgents()
    {
        // Arrange
        var request = new DiscoverAgentsRequest(Capability: null, Status: AgentStatus.Active);
        
        var expectedAgents = new[]
        {
            TestDataBuilder.CreateTestAgent(status: AgentStatus.Active),
            TestDataBuilder.CreateTestAgent(status: AgentStatus.Active)
        };

        _mockRegistry
            .GetAllAgentsAsync(AgentStatus.Active, Arg.Any<CancellationToken>())
            .Returns(expectedAgents);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.Agents.Length.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.Agents.ShouldAllBe(a => a.Status == AgentStatus.Active);
    }

    [Fact]
    public async Task HandleAsync_WithoutFilters_ShouldReturnAllAgents()
    {
        // Arrange
        var request = new DiscoverAgentsRequest(Capability: null, Status: null);
        
        var expectedAgents = new[]
        {
            TestDataBuilder.CreateTestAgent(),
            TestDataBuilder.CreateTestAgent(),
            TestDataBuilder.CreateTestAgent()
        };

        _mockRegistry
            .GetAllAgentsAsync(null, Arg.Any<CancellationToken>())
            .Returns(expectedAgents);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.Agents.Length.ShouldBe(3);
        result.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task HandleAsync_WhenNoAgentsFound_ShouldReturnEmptyArray()
    {
        // Arrange
        var request = new DiscoverAgentsRequest(Capability: "non-existent", Status: null);
        
        _mockRegistry
            .FindAgentsByCapabilityAsync("non-existent", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Agent>());

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.Agents.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }
}
