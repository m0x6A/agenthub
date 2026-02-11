Feature: Direct Agent-to-Agent Messaging
  As an autonomous agent
  I want to send and receive direct messages to/from other agents
  So that I can coordinate tasks and share information

  Background:
    Given the AgentBus broker is running
    And agent "sender-agent-001" is registered
    And agent "receiver-agent-002" is registered

  Scenario: Send a direct message between agents
    When agent "sender-agent-001" sends a message to "receiver-agent-002":
      | Field       | Value                      |
      | MessageType | command.logistics.schedule |
      | Payload     | {"droneId": "drone-42"}    |
    Then the message should be sent successfully
    And a message ID should be returned

  Scenario: Receive a pending message
    Given agent "sender-agent-001" has sent a message to "receiver-agent-002"
    When agent "receiver-agent-002" receives messages with timeout 5 seconds
    Then a message should be received
    And the message should be from "sender-agent-001"

  Scenario: Acknowledge received message
    Given agent "receiver-agent-002" has received a message
    When agent "receiver-agent-002" acknowledges the message
    Then the acknowledgment should succeed
    And the message should not be redelivered

  Scenario: Message with correlation ID for request-response
    When agent "sender-agent-001" sends a message with correlation ID "req-12345"
    And agent "receiver-agent-002" receives the message
    Then the received message should have correlation ID "req-12345"

  Scenario: Message times out after TTL expires
    When agent "sender-agent-001" sends a message with TTL of 1 second
    And I wait 2 seconds
    When agent "receiver-agent-002" tries to receive messages
    Then the message should have moved to dead-letter queue

  Scenario: Send message to non-existent agent fails
    When agent "sender-agent-001" sends a message to "non-existent-agent"
    Then the message sending should fail
    And the error should indicate agent not found

  Scenario: Long-polling receive with no messages
    When agent "receiver-agent-002" receives messages with timeout 2 seconds
    And no messages are pending
    Then no message should be received
    And the response should indicate no content
