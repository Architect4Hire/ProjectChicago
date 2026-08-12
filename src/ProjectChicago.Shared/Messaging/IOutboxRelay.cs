namespace ProjectChicago.Shared.Messaging;

// Reusable relay mechanism a timer-triggered Function delegates to (functions.md: "calls a reusable
// relay service for its own service database... does not contain polling SQL or event-specific
// business logic"). Not a Function, HostedService, or BackgroundService itself - just the orchestration
// this project owns so every publishing service's Functions project can stay a thin trigger.
public interface IOutboxRelay
{
    Task<OutboxRelayResult> RelayPendingAsync(OutboxRelayOptions options, CancellationToken cancellationToken = default);
}
