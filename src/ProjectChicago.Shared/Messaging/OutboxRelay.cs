using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using ProjectChicago.Shared.Outbox;

namespace ProjectChicago.Shared.Messaging;

// Default IOutboxRelay: claim a bounded, already-leased batch from the owning service's store,
// publish each message through the shared publisher, and settle it - dispatched only after a
// confirmed publish, left pending (retryable) on any publish failure. Contains no contract-specific
// branching: every OutboxMessage is forwarded to Service Bus by its already-serialized Payload alone
// (OUTBOX-003..006, ASYNC-005..008).
public sealed class OutboxRelay : IOutboxRelay
{
    private static readonly Meter Meter = new("ProjectChicago.Shared.Outbox.Relay");
    private static readonly Counter<long> ClaimedCounter = Meter.CreateCounter<long>("outbox.relay.claimed");
    private static readonly Counter<long> DispatchedCounter = Meter.CreateCounter<long>("outbox.relay.dispatched");
    private static readonly Counter<long> FailedCounter = Meter.CreateCounter<long>("outbox.relay.failed");

    private readonly IOutboxStore _store;
    private readonly IServiceBusPublisher _publisher;
    private readonly ILogger<OutboxRelay> _logger;

    public OutboxRelay(IOutboxStore store, IServiceBusPublisher publisher, ILogger<OutboxRelay> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OutboxRelayResult> RelayPendingAsync(OutboxRelayOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var batch = await _store.ClaimPendingBatchAsync(
            options.BatchSize, options.LeaseOwner, options.LeaseDuration, cancellationToken).ConfigureAwait(false);

        ClaimedCounter.Add(batch.Count);

        if (batch.Count == 0)
        {
            _logger.LogDebug("Outbox relay found no pending messages to dispatch.");
            return new OutboxRelayResult { ClaimedCount = 0, DispatchedCount = 0, FailedCount = 0 };
        }

        var dispatchedCount = 0;
        var failedCount = 0;

        foreach (var message in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await TryDispatchAsync(message, options.EntityName, cancellationToken).ConfigureAwait(false))
            {
                dispatchedCount++;
            }
            else
            {
                failedCount++;
            }
        }

        return new OutboxRelayResult
        {
            ClaimedCount = batch.Count,
            DispatchedCount = dispatchedCount,
            FailedCount = failedCount,
        };
    }

    private async Task<bool> TryDispatchAsync(OutboxMessage message, string entityName, CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishAsync(entityName, ToOutboundMessage(message), cancellationToken).ConfigureAwait(false);
            await _store.MarkDispatchedAsync(message.Id, cancellationToken).ConfigureAwait(false);

            DispatchedCounter.Add(1);
            _logger.LogInformation(
                "Outbox message {MessageId} ({ContractType} v{ContractVersion}) dispatched. CorrelationId={CorrelationId}",
                message.Id, message.ContractType, message.ContractVersion, message.CorrelationId);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            FailedCounter.Add(1);
            _logger.LogWarning(
                ex,
                "Outbox message {MessageId} ({ContractType} v{ContractVersion}) failed to dispatch and remains pending. CorrelationId={CorrelationId}",
                message.Id, message.ContractType, message.ContractVersion, message.CorrelationId);

            await _store.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);

            return false;
        }
    }

    private static OutboundServiceBusMessage ToOutboundMessage(OutboxMessage message) => new()
    {
        MessageId = message.Id.ToString(),
        ContractType = message.ContractType,
        ContractVersion = message.ContractVersion,
        CorrelationId = message.CorrelationId,
        CausationId = message.CausationId,
        TraceId = message.TraceId,
        Body = message.Payload,
    };
}
