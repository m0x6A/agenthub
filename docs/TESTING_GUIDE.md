# Testing Guide - AgentBus

This guide describes the comprehensive testing strategy for the AgentBus platform.

## Overview

The AgentBus test suite includes:
- **BDD Tests** (Behavior-Driven Development using Reqnroll/Gherkin)
- **Unit Tests** (Handler-level tests using xUnit)
- **Integration Tests** (API-level tests using WebApplicationFactory)

## Test Statistics

- **Total Unit Tests**: 22
- **Pass Rate**: 100%
- **BDD Scenarios**: 21 scenarios across 3 feature files
- **Modules Covered**: Registration, Messaging, Eventing

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Only Unit Tests
```bash
dotnet test --filter "FullyQualifiedName~Unit"
```

### Run Only BDD Tests
```bash
dotnet test --filter "FullyQualifiedName~Features"
```

### Run Tests with Coverage
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

### Run Specific Module Tests
```bash
# Registration tests only
dotnet test --filter "FullyQualifiedName~Registration"

# Messaging tests only  
dotnet test --filter "FullyQualifiedName~Messaging"

# Eventing tests only
dotnet test --filter "FullyQualifiedName~Eventing"
```

## Test Structure

### Unit Tests

Location: `tests/AgentBus.Broker.Tests/Unit/`

Each handler has corresponding unit tests:

**Registration Module**
- `RegisterAgentHandlerTests.cs` - Tests agent registration logic
- `DiscoverAgentsHandlerTests.cs` - Tests agent discovery logic

**Messaging Module**
- `SendMessageHandlerTests.cs` - Tests message sending logic
- `ReceiveMessageHandlerTests.cs` - Tests message receiving logic

**Eventing Module**
- `PublishEventHandlerTests.cs` - Tests event publishing logic
- `SubscribeToEventHandlerTests.cs` - Tests event subscription logic

### BDD Tests

Location: `tests/AgentBus.Broker.Tests/Features/`

**Registration.feature** - 7 scenarios
- Successfully register a new agent
- Reject registration with invalid version format
- Discover agents by capability
- Update agent heartbeat
- Deregister an agent
- Agent becomes inactive after missed heartbeats
- Register agent with custom metadata

**Messaging.feature** - 7 scenarios
- Send a direct message between agents
- Receive a pending message
- Acknowledge received message
- Message with correlation ID for request-response
- Message times out after TTL expires
- Send message to non-existent agent fails
- Long-polling receive with no messages

**Eventing.feature** - 7 scenarios
- Publish an event
- Subscribe to an event type
- Receive published events
- Multiple subscribers receive fan-out events
- Unsubscribe from event type
- Subscribe with SQL filters
- Event with custom headers

## Test Helpers

### TestWebApplicationFactory

Located in `tests/AgentBus.Broker.Tests/Helpers/TestWebApplicationFactory.cs`

Creates a test instance of the web application with mocked dependencies:
- `MockAgentRegistry` - Mocked IAgentRegistry
- `MockMessageBroker` - Mocked IMessageBroker
- `MockEventBroker` - Mocked IEventBroker

Usage:
```csharp
using var factory = new TestWebApplicationFactory();
var client = factory.CreateClient();

// Configure mock behaviors
factory.MockAgentRegistry.GetAgentByIdAsync(agentId, Arg.Any<CancellationToken>())
    .Returns(testAgent);

// Make HTTP requests
var response = await client.GetAsync("/api/v1/agents");
```

### TestDataBuilder

Located in `tests/AgentBus.Broker.Tests/Helpers/TestDataBuilder.cs`

Provides methods to create test data using Bogus:

```csharp
// Create test agent
var agent = TestDataBuilder.CreateTestAgent(
    id: "custom-id",
    capabilities: new[] { "test-capability" },
    status: AgentStatus.Active);

// Create test message
var message = TestDataBuilder.CreateTestMessage(
    from: "sender-agent",
    to: "receiver-agent");

// Create test event
var event = TestDataBuilder.CreateTestEvent(
    source: "publisher-agent",
    eventType: "events.test.published");

// Create test subscription
var subscription = TestDataBuilder.CreateTestSubscription(
    agentId: "subscriber-agent",
    eventType: "events.test.event");
```

## Writing New Tests

### Adding a Unit Test

1. Create a new test class in the appropriate module folder
2. Name it `<Handler>Tests.cs`
3. Follow the AAA pattern (Arrange, Act, Assert)
4. Use NSubstitute for mocking
5. Use Shouldly for assertions

Example:
```csharp
using Xunit;
using Shouldly;
using NSubstitute;

namespace AgentBus.Broker.Tests.Unit.Registration;

public class MyHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidInput_ShouldSucceed()
    {
        // Arrange
        var mockDependency = Substitute.For<IDependency>();
        var handler = new MyHandler(mockDependency);
        
        // Act
        var result = await handler.HandleAsync(request, CancellationToken.None);
        
        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
    }
}
```

### Adding a BDD Scenario

1. Add scenario to the appropriate `.feature` file
2. Use Given/When/Then format
3. Create corresponding step definitions in `StepDefinitions/`
4. Use TestWebApplicationFactory for integration testing

Example:
```gherkin
Scenario: Successfully process a request
    Given the service is running
    And the database contains test data
    When I submit a valid request
    Then the request should be processed successfully
    And the result should be stored in the database
```

## Continuous Integration

The GitHub Actions workflow `.github/workflows/pr-check.yml` automatically:
- Runs all tests on every PR
- Collects code coverage
- Posts coverage reports as PR comments
- Uploads coverage to Codecov
- Publishes test results

## Code Coverage

Current coverage targets:
- **Minimum**: 60% (PR will show warning)
- **Target**: 80% (PR will show success)

Generate local coverage report:
```bash
dotnet test --collect:"XPlat Code Coverage"
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"coverage/**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

Then open `coveragereport/index.html` in your browser.

## Best Practices

1. **Test Isolation**: Each test should be independent
2. **Clear Naming**: Test names should describe what they test
3. **AAA Pattern**: Arrange, Act, Assert
4. **Mock External Dependencies**: Use NSubstitute for all external dependencies
5. **Realistic Test Data**: Use TestDataBuilder for consistent test data
6. **Assert Clearly**: Use Shouldly for readable assertions
7. **Test Edge Cases**: Include tests for error conditions
8. **Keep Tests Fast**: Unit tests should run in milliseconds

## Troubleshooting

### Tests Fail Locally But Pass in CI
- Ensure you're on the same .NET version (9.0)
- Clear test artifacts: `dotnet clean && dotnet build`
- Check for hardcoded paths or environment-specific values

### BDD Step Not Found
- Ensure step definition is in `StepDefinitions/` folder
- Check that method signature matches the step text exactly
- Rebuild the test project

### Mock Returns Wrong Type
- Verify the mocked method signature
- Use `.Returns(Task.FromResult(value))` for async methods
- Use `.Returns(value)` for non-async methods

## Resources

- [xUnit Documentation](https://xunit.net/)
- [Reqnroll Documentation](https://docs.reqnroll.net/)
- [NSubstitute Documentation](https://nsubstitute.github.io/)
- [Shouldly Documentation](https://docs.shouldly.org/)
- [Bogus Documentation](https://github.com/bchavez/Bogus)
