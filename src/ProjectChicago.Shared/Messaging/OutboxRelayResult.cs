namespace ProjectChicago.Shared.Messaging;

// Summary of one relay run, for the timer Function to log/emit as its own invocation-level metrics
// (OBS-005: Function execution success/failure, Service Bus processing failures) alongside the
// structured logs/metrics OutboxRelay emits internally per message.
public sealed record OutboxRelayResult
{
    public required int ClaimedCount { get; init; }

    public required int DispatchedCount { get; init; }

    public required int FailedCount { get; init; }
}
