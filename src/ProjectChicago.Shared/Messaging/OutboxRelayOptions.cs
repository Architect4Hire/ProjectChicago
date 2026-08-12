namespace ProjectChicago.Shared.Messaging;

// Caller-supplied (the owning service's timer-triggered Function) configuration for one relay run.
// Entity name, batch size, lease duration and the owner identity are all configuration/operational
// settings, never hardcoded inside OutboxRelay (messaging.md, functions.md).
public sealed record OutboxRelayOptions
{
    public required string EntityName { get; init; }

    public required int BatchSize { get; init; }

    public required TimeSpan LeaseDuration { get; init; }

    // Identifies this relay invocation/instance for the store's lease-claim strategy - e.g. a
    // Function invocation ID - so a crashed/timed-out invocation's lease can be safely reclaimed by
    // a later one.
    public required string LeaseOwner { get; init; }
}
