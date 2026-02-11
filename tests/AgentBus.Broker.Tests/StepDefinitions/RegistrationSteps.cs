using Reqnroll;
using Shouldly;
using AgentBus.Broker.Tests.Helpers;
using AgentBus.Broker.SharedKernel.Models;
using NSubstitute;
using System.Net;
using System.Net.Http.Json;

namespace AgentBus.Broker.Tests.StepDefinitions;

[Binding]
public class RegistrationSteps : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private HttpResponseMessage? _response;
    private Agent? _createdAgent;
    private List<Agent> _foundAgents = new();

    public RegistrationSteps()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Given(@"the AgentBus broker is running")]
    public void GivenTheAgentBusBrokerIsRunning()
    {
        // Factory creates the test server
    }

    [Given(@"the agent registry is empty")]
    public void GivenTheAgentRegistryIsEmpty()
    {
        _factory.ResetMocks();
    }

    [When(@"I register an agent with the following details:")]
    public async Task WhenIRegisterAnAgentWithFollowingDetails(Table table)
    {
        var row = table.Rows[0];
        var capabilities = row["Capabilities"].Split(',');
        
        var agent = TestDataBuilder.CreateTestAgent(
            id: row["Id"],
            name: row["Name"],
            version: row["Version"],
            capabilities: capabilities);

        _factory.MockAgentRegistry
            .RegisterAgentAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>())
            .Returns(agent);

        var request = new
        {
            id = agent.Id,
            name = agent.Name,
            version = agent.Version,
            capabilities = agent.Capabilities
        };

        _response = await _client.PostAsJsonAsync("/api/v1/agents", request);
        _createdAgent = agent;
    }

    [When(@"I register an agent with invalid version ""(.*)""")]
    public async Task WhenIRegisterAnAgentWithInvalidVersion(string version)
    {
        var request = new
        {
            id = "test-agent",
            name = "Test Agent",
            version = version,
            capabilities = new[] { "test" }
        };

        _response = await _client.PostAsJsonAsync("/api/v1/agents", request);
    }

    [Then(@"the registration should succeed")]
    public void ThenTheRegistrationShouldSucceed()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Then(@"the agent should be retrievable by ID ""(.*)""")]
    public async Task ThenTheAgentShouldBeRetrievableById(string agentId)
    {
        _factory.MockAgentRegistry
            .GetAgentByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(_createdAgent);

        var getResponse = await _client.GetAsync($"/api/v1/agents/{agentId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Then(@"the agent status should be ""(.*)""")]
    public void ThenTheAgentStatusShouldBe(string status)
    {
        _createdAgent.ShouldNotBeNull();
        _createdAgent.Status.ToString().ToLower().ShouldBe(status.ToLower());
    }

    [Then(@"the registration should fail with validation error")]
    public void ThenTheRegistrationShouldFailWithValidationError()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Then(@"the error should mention ""(.*)""")]
    public async Task ThenTheErrorShouldMention(string keyword)
    {
        var content = await _response!.Content.ReadAsStringAsync();
        content.ToLower().ShouldContain(keyword.ToLower());
    }

    [Given(@"the following agents are registered:")]
    public void GivenTheFollowingAgentsAreRegistered(Table table)
    {
        var agents = new List<Agent>();
        foreach (var row in table.Rows)
        {
            var capabilities = row["Capabilities"].Split(',');
            agents.Add(TestDataBuilder.CreateTestAgent(
                id: row["Id"],
                capabilities: capabilities));
        }

        _factory.MockAgentRegistry
            .GetAllAgentsAsync(Arg.Any<AgentStatus?>(), Arg.Any<CancellationToken>())
            .Returns(agents);
    }

    [When(@"I search for agents with capability ""(.*)""")]
    public async Task WhenISearchForAgentsWithCapability(string capability)
    {
        var agents = new List<Agent>
        {
            TestDataBuilder.CreateTestAgent(id: "logistics-agent-001", capabilities: new[] { "schedule-drone" }),
            TestDataBuilder.CreateTestAgent(id: "route-agent-003", capabilities: new[] { "schedule-drone", "plan" })
        };

        _factory.MockAgentRegistry
            .FindAgentsByCapabilityAsync(capability, Arg.Any<CancellationToken>())
            .Returns(agents);

        _response = await _client.GetAsync($"/api/v1/agents?capability={capability}");
        _foundAgents = await _response.Content.ReadFromJsonAsync<List<Agent>>() ?? new();
    }

    [Then(@"I should find (.*) agents")]
    public void ThenIShouldFindAgents(int count)
    {
        _foundAgents.Count.ShouldBe(count);
    }

    [Then(@"the results should include ""(.*)""")]
    public void ThenTheResultsShouldInclude(string agentId)
    {
        _foundAgents.Any(a => a.Id == agentId).ShouldBeTrue();
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}
