using AgentBus.Broker.SharedKernel.Models;

namespace AgentBus.Broker.Modules.Eventing.Features.ReceiveEvent;

public sealed record ReceiveEventResponse(EventEnvelope? Event);
