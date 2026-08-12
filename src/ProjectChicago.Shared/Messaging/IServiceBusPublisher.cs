namespace ProjectChicago.Shared.Messaging;

// Abstraction the timer-triggered outbox relay (owning service's Functions project) publishes
// through. It accepts an already-serialized envelope and the caller-resolved entity name; it does
// not query outbox rows, decide dispatch state, or resolve the destination itself - the relay does
// both of those against its own service database and its own service's configuration
// (functions.md: "publishes pending outbox messages through the shared Service Bus publisher").
public interface IServiceBusPublisher
{
    Task PublishAsync(string entityName, OutboundServiceBusMessage message, CancellationToken cancellationToken = default);
}
