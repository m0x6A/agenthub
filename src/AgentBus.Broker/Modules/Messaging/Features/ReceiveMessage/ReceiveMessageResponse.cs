using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.Modules.Messaging.Features.ReceiveMessage;

public sealed record ReceiveMessageResponse(MessageEnvelope? Message);
