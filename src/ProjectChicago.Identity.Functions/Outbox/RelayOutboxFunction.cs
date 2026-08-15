using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectChicago.Shared.Messaging;

namespace ProjectChicago.Identity.Functions.Outbox;

// Timer-triggered outbox relay entry point for Identity (OUTBOX-003..006, ASYNC-001..008; functions.md
// "Timer trigger - outbox relay"). Schedule, entity name, batch size and lease duration all come from
// configuration (messaging.md). This class contains no polling SQL and no event-specific branching -
// IOutboxRelay (ProjectChicago.Shared) owns batch claim/publish/settle against Identity's own outbox
// store; this trigger only schedules the run and delegates.
//
// LeaseOwner is a fresh Guid per invocation - IOutboxRelay only needs a value that uniquely
// identifies *this* run so a crashed/timed-out lease can be safely reclaimed later (IOutboxStore
// doc); a Function invocation ID would serve the same purpose but would require taking a
// FunctionContext dependency this trigger otherwise has no use for.
//
// IOutboxRelay/IServiceBusPublisher/IOutboxStore are registered in Program.cs, backed by
// IdentityOutboxStore (ProjectChicago.Identity.Core.Persistence) against IdentityDbContext.
public sealed class RelayOutboxFunction
{
    private readonly IOutboxRelay _relay;
    private readonly OutboxRelaySettings _settings;
    private readonly ILogger<RelayOutboxFunction> _logger;

    public RelayOutboxFunction(IOutboxRelay relay, IOptions<OutboxRelaySettings> settings, ILogger<RelayOutboxFunction> logger)
    {
        _relay = relay ?? throw new ArgumentNullException(nameof(relay));
        _settings = settings is null ? throw new ArgumentNullException(nameof(settings)) : settings.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(RelayOutboxFunction))]
    public async Task RunAsync(
        [TimerTrigger("%Identity:OutboxRelay:Schedule%")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var options = new OutboxRelayOptions
        {
            EntityName = _settings.EntityName,
            BatchSize = _settings.BatchSize,
            LeaseDuration = _settings.LeaseDuration,
            LeaseOwner = Guid.NewGuid().ToString(),
        };

        var result = await _relay.RelayPendingAsync(options, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Identity outbox relay claimed {ClaimedCount}, dispatched {DispatchedCount}, failed {FailedCount}.",
            result.ClaimedCount, result.DispatchedCount, result.FailedCount);
    }
}
