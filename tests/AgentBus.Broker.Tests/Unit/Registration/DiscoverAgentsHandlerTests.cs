using AgentBus.Broker.Modules.Registration.Features.DiscoverAgents;
using AgentBus.Broker.SharedKernel.Interfaces;
using AgentBus.Broker.SharedKernel.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AgentBus.Broker.Tests.Unit.Registration;

[Trait("Category", "Registration")]
public sealed class DiscoverAgentsHandlerTests
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly DiscoverAgentsHandler _handler;

    public DiscoverAgentsHandlerTests()
    {
        _agentRegistry = Substitute.For<IAgentRegistry>();
        _handler = new DiscoverAgentsHandler(_agentRegistry);
    }

    [Fact]
    public async Task Handle_NoFilters_ReturnsAllActiveAgents()
    {
        // Arrange
        var agents = new[]
        {
            CreateAgent("agent-1", "Agent 1", AgentStatus.Active),
            CreateAgent("agent-2", "Agent 2", AgentStatus.Active),
            CreateAgent("agent-3", "Agent 3", AgentStatus.Inactive)
        };

        _agentRegistry.GetAllAgentsAsync(null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<Agent>>(agents.Where(a => a.Status == AgentStatus.Active).ToArray()));

        var request = new DiscoverAgentsRequest(Capability: null, Status: null);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.Agents.Length.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.Agents.ShouldContain(a => a.Id == "agent-1");
        result.Agents.ShouldContain(a => a.Id == "agent-2");
    }

    [Fact]
    public async Task Handle_WithCapabilityFilter_ReturnsMatchingAgents()
    {
        // Arrange
        var agents = new[]
        {
            CreateAgent("agent-1", "Agent 1", AgentStatus.Active, new[] { "schedule-drone", "assign-route" }),
            CreateAgent("agent-2", "Agent 2", AgentStatus.Active, new[] { "check-weather" }),
            CreateAgent("agent-3", "Agent 3", AgentStatus.Active, new[] { "schedule-drone", "calculate-eta" })
        };

        _agentRegistry.FindAgentsByCapabilityAsync("schedule-drone", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<Agent>>(agents.Where(a => a.Capabilities.Contains("schedule-drone")).ToArray()));

        var request = new DiscoverAgentsRequest(Capability: "schedule-drone", Status: null);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.Agents.Length.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.Agents.ShouldContain(a => a.Id == "agent-1");
        result.Agents.ShouldContain(a => a.Id == "agent-3");
        result.Agents.ShouldNotContain(a => a.Id == "agent-2");
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ReturnsOnlyAgentsWithStatus()
    {
        // Arrange
        var agents = new[]
        {
            CreateAgent("agent-1", "Agent 1", AgentStatus.Active),
            CreateAgent("agent-2", "Agent 2", AgentStatus.Inactive),
            CreateAgent("agent-3", "Agent 3", AgentStatus.Active)
        };

        _agentRegistry.GetAllAgentsAsync(AgentStatus.Active, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<Agent>>(agents.Where(a => a.Status == AgentStatus.Active).ToArray()));

        var request = new DiscoverAgentsRequest(Capability: null, Status: AgentStatus.Active);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.Agents.Length.ShouldBe(2);
        result.Agents.ShouldAllBe(a => a.Status == AgentStatus.Active);
    }

    [Fact]
    public async Task Handle_NoMatchingAgents_ReturnsEmptyList()
    {
        // Arrange
        _agentRegistry.FindAgentsByCapabilityAsync("non-existent-capability", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<Agent>>(Array.Empty<Agent>()));

        var request = new DiscoverAgentsRequest(Capability: "non-existent-capability", Status: null);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        result.Agents.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_CancellationToken_PropagatedToRegistry()
    {
        // Arrange
        var request = new DiscoverAgentsRequest(Capability: null, Status: null);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _agentRegistry.GetAllAgentsAsync(null, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var token = callInfo.Arg<CancellationToken>();
                token.ThrowIfCancellationRequested();
                return Task.FromResult(Array.Empty<Agent>());
            });

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _handler.HandleAsync(request, cts.Token));
    }

    private static Agent CreateAgent(string id, string name, AgentStatus status, string[]? capabilities = null)
    {
        return new Agent(
            Id: id,
            PartitionKey: id,
            Name: name,
            Version: "1.0.0",
            Status: status,
            Capabilities: capabilities ?? new[] { "default-capability" },
            MessageTypes: new MessageTypes(new[] { "*" }, new[] { "*" }),
            Identity: new AgentIdentity($"/subscriptions/test/mi/{id}", $"principal-{id}", "tenant-123"),
            Endpoints: new AgentEndpoints($"agent-{id}-inbox", null),
            Metadata: null,
            Timestamps: new AgentTimestamps(DateTime.UtcNow, DateTime.UtcNow)
        );
    }
}
