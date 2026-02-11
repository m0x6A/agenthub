Feature: Agent Registration & Discovery
  As an autonomous agent
  I want to register with the AgentBus broker and discover other agents
  So that I can communicate with agents that have specific capabilities

  Background:
    Given the AgentBus broker is running
    And I have a valid Managed Identity with "AgentBus.Agent" role

  Scenario: New agent successfully registers with the broker
    Given I am a new agent with the following details:
      | Field        | Value                                       |
      | name         | Logistics Coordinator                       |
      | version      | 1.0.0                                       |
      | capabilities | schedule-drone,assign-route,calculate-eta   |
    When I send a POST request to "/api/v1/agents" with my registration details
    Then I receive a 201 Created response
    And the response contains my agent ID
    And the response contains my inbox queue name
    And I can retrieve my registration via GET "/api/v1/agents/{myId}"
    And my status is "active"

  Scenario: Registered agent sends periodic heartbeats
    Given I am a registered agent with ID "test-agent-123"
    And my last heartbeat was "2026-02-11T10:00:00Z"
    When I send a PATCH request to "/api/v1/agents/test-agent-123/heartbeat"
    Then I receive a 200 OK response
    And my "lastHeartbeat" timestamp is updated
    And my status remains "active"

  Scenario: Query agents by capability returns matching agents
    Given the following agents are registered:
      | AgentId              | Name                 | Capabilities                              |
      | logistics-agent-1    | Logistics Agent 1    | schedule-drone,assign-route               |
      | weather-agent-1      | Weather Agent 1      | check-weather,forecast                    |
      | logistics-agent-2    | Logistics Agent 2    | schedule-drone,calculate-eta              |
    When I send a GET request to "/api/v1/agents?capability=schedule-drone"
    Then I receive a 200 OK response
    And the response contains 2 agents
    And the agents list includes "logistics-agent-1"
    And the agents list includes "logistics-agent-2"
    And the agents list does not include "weather-agent-1"

  Scenario: Registered agent deregisters successfully
    Given I am a registered agent with ID "test-agent-456"
    And I have an inbox queue "agent-test-agent-456-inbox"
    When I send a DELETE request to "/api/v1/agents/test-agent-456"
    Then I receive a 204 No Content response
    And my registration is removed from the registry
    And my inbox queue is deleted from Service Bus

  Scenario: Unauthenticated caller cannot register
    Given I do not have a valid Managed Identity token
    When I attempt to send a POST request to "/api/v1/agents" with registration details
    Then I receive a 401 Unauthorized response
    And no agent is created in the registry

  Scenario: Agent with missing heartbeats becomes inactive
    Given I am a registered agent with ID "test-agent-789"
    And my last heartbeat was "2026-02-11T10:00:00Z"
    And the current time is "2026-02-11T10:06:00Z"
    When the heartbeat monitor service runs
    Then my status is updated to "inactive"
    And I can be reactivated by sending a heartbeat

  Scenario: Agent registration with invalid version format fails
    Given I am a new agent with an invalid version "v1.0"
    When I send a POST request to "/api/v1/agents" with my registration details
    Then I receive a 400 Bad Request response
    And the response contains a validation error for "version"

  Scenario: Agent registration with duplicate ID fails
    Given an agent with ID "duplicate-agent-id" already exists
    When I attempt to register with the same ID "duplicate-agent-id"
    Then I receive a 409 Conflict response
    And the response indicates the agent ID already exists

  Scenario: Query all agents without filters returns all active agents
    Given the following agents are registered:
      | AgentId              | Name                 | Status   |
      | agent-1              | Agent 1              | active   |
      | agent-2              | Agent 2              | active   |
      | agent-3              | Agent 3              | inactive |
    When I send a GET request to "/api/v1/agents"
    Then I receive a 200 OK response
    And the response contains agents with status "active"

  Scenario: Get specific agent by ID returns agent details
    Given I am a registered agent with ID "specific-agent-123"
    When I send a GET request to "/api/v1/agents/specific-agent-123"
    Then I receive a 200 OK response
    And the response contains my full agent details
    And the response includes my capabilities
    And the response includes my endpoints
