Feature: Event Publishing and Subscription
  As an autonomous agent
  I want to publish domain events and subscribe to events from other agents
  So that I can react to system-wide changes and notifications

  Background:
    Given the AgentBus broker is running
    And agent "publisher-agent-001" is registered
    And agent "subscriber-agent-002" is registered

  Scenario: Publish an event
    When agent "publisher-agent-001" publishes an event:
      | Field       | Value                          |
      | EventType   | events.logistics.drone-scheduled |
      | DataVersion | 1.0.0                          |
      | Data        | {"droneId": "drone-42"}        |
    Then the event should be published successfully
    And an event ID should be returned

  Scenario: Subscribe to an event type
    When agent "subscriber-agent-002" subscribes to event type "events.logistics.drone-scheduled"
    Then the subscription should succeed
    And a subscription ID should be returned

  Scenario: Receive published events
    Given agent "subscriber-agent-002" is subscribed to "events.logistics.drone-scheduled"
    When agent "publisher-agent-001" publishes event "events.logistics.drone-scheduled"
    And agent "subscriber-agent-002" receives events from their subscription
    Then an event should be received
    And the event should be from "publisher-agent-001"

  Scenario: Multiple subscribers receive fan-out events
    Given agent "subscriber-agent-002" is subscribed to "events.weather.alert"
    And agent "subscriber-agent-003" is subscribed to "events.weather.alert"
    When agent "publisher-agent-001" publishes event "events.weather.alert"
    Then both "subscriber-agent-002" and "subscriber-agent-003" should receive the event

  Scenario: Unsubscribe from event type
    Given agent "subscriber-agent-002" is subscribed to "events.logistics.drone-scheduled"
    When agent "subscriber-agent-002" unsubscribes from the subscription
    Then the unsubscription should succeed
    And agent "subscriber-agent-002" should not receive new events of that type

  Scenario: Subscribe with SQL filters
    When agent "subscriber-agent-002" subscribes to "events.weather.alert" with filters:
      | Filter           |
      | severity='high'  |
      | region='west'    |
    Then the subscription should succeed with filters
    And only matching events should be received

  Scenario: Event with custom headers
    When agent "publisher-agent-001" publishes an event with headers:
      | Header          | Value              |
      | x-trace-id      | trace-123          |
      | x-correlation-id| corr-456           |
    Then the event should include custom headers
