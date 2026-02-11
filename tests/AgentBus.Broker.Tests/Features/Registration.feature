Feature: Agent Registration and Discovery
  As an autonomous agent
  I want to register with the broker and discover other agents
  So that I can participate in the agent communication network

  Background:
    Given the AgentBus broker is running
    And the agent registry is empty

  Scenario: Successfully register a new agent
    When I register an agent with the following details:
      | Field        | Value                              |
      | Id           | logistics-agent-001                |
      | Name         | Logistics Coordinator              |
      | Version      | 1.0.0                              |
      | Capabilities | schedule-drone,assign-route        |
    Then the registration should succeed
    And the agent should be retrievable by ID "logistics-agent-001"
    And the agent status should be "active"

  Scenario: Reject registration with invalid version format
    When I register an agent with invalid version "not-semver"
    Then the registration should fail with validation error
    And the error should mention "version"

  Scenario: Discover agents by capability
    Given the following agents are registered:
      | Id                  | Capabilities        |
      | logistics-agent-001 | schedule-drone      |
      | weather-agent-002   | forecast-weather    |
      | route-agent-003     | schedule-drone,plan |
    When I search for agents with capability "schedule-drone"
    Then I should find 2 agents
    And the results should include "logistics-agent-001"
    And the results should include "route-agent-003"

  Scenario: Update agent heartbeat
    Given an agent "test-agent-001" is registered
    And the agent's last heartbeat was 2 minutes ago
    When I update the heartbeat for agent "test-agent-001"
    Then the heartbeat should be updated successfully
    And the agent status should remain "active"

  Scenario: Deregister an agent
    Given an agent "temp-agent-001" is registered
    When I deregister agent "temp-agent-001"
    Then the deregistration should succeed
    And the agent should no longer be retrievable

  Scenario: Agent becomes inactive after missed heartbeats
    Given an agent "inactive-agent-001" is registered
    And the agent's last heartbeat was 6 minutes ago
    When the heartbeat monitor runs
    Then the agent status should be "inactive"

  Scenario: Register agent with custom metadata
    When I register an agent with metadata:
      | Field       | Value                    |
      | Owner       | [email protected] |
      | Environment | production               |
      | Tags        | critical,high-priority   |
    Then the registration should succeed
    And the agent metadata should be stored correctly
