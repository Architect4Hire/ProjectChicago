using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectChicago.Shared.Messaging;
using ProjectChicago.Shared.Outbox;
using Xunit;

namespace ProjectChicago.Shared.Tests;

public class OutboxRelayTests
{
    private sealed class FakeOutboxStore : IOutboxStore
    {
        public Queue<IReadOnlyList<OutboxMessage>> BatchesToReturn { get; } = new();

        public int BatchSizeRequested { get; private set; }

        public string? LeaseOwnerRequested { get; private set; }

        public TimeSpan LeaseDurationRequested { get; private set; }

        public List<Guid> DispatchedMessageIds { get; } = [];

        public List<(Guid MessageId, string Error)> FailedAttempts { get; } = [];

        public Task<IReadOnlyList<OutboxMessage>> ClaimPendingBatchAsync(
            int batchSize, string leaseOwner, TimeSpan leaseDuration, CancellationToken cancellationToken)
        {
            BatchSizeRequested = batchSize;
            LeaseOwnerRequested = leaseOwner;
            LeaseDurationRequested = leaseDuration;

            var batch = BatchesToReturn.Count > 0 ? BatchesToReturn.Dequeue() : [];
            return Task.FromResult(batch);
        }

        public Task MarkDispatchedAsync(Guid messageId, CancellationToken cancellationToken)
        {
            DispatchedMessageIds.Add(messageId);
            return Task.CompletedTask;
        }

        public Task RecordFailedAttemptAsync(Guid messageId, string error, CancellationToken cancellationToken)
        {
            FailedAttempts.Add((messageId, error));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeServiceBusPublisher : IServiceBusPublisher
    {
        public List<OutboundServiceBusMessage> PublishedMessages { get; } = [];

        // Keyed by MessageId - message IDs listed here throw instead of succeeding.
        public HashSet<string> MessageIdsThatFail { get; } = [];

        public string? EntityNameRequested { get; private set; }

        public Task PublishAsync(string entityName, OutboundServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            EntityNameRequested = entityName;

            if (MessageIdsThatFail.Contains(message.MessageId))
            {
                throw new InvalidOperationException($"Simulated publish failure for {message.MessageId}.");
            }

            PublishedMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private static OutboxMessage CreateMessage(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        ContractType = "Audit.EntityMutationAudited",
        ContractVersion = 1,
        Payload = "{\"eventId\":\"event-1\"}",
        CorrelationId = "correlation-1",
        CausationId = "causation-1",
        TraceId = "4bf92f3577b34da6a3ce929d0e0e4736",
        OccurredAtUtc = new DateTime(2026, 8, 12, 9, 15, 0, DateTimeKind.Utc),
        CreatedAtUtc = new DateTime(2026, 8, 12, 9, 15, 0, DateTimeKind.Utc),
    };

    private static OutboxRelayOptions CreateOptions() => new()
    {
        EntityName = "ProjectChicago.Events",
        BatchSize = 25,
        LeaseDuration = TimeSpan.FromMinutes(1),
        LeaseOwner = "relay-instance-1",
    };

    private static OutboxRelay CreateRelay(FakeOutboxStore store, IServiceBusPublisher publisher) =>
        new(store, publisher, NullLogger<OutboxRelay>.Instance);

    [Fact]
    public async Task RelayPendingAsync_EmptyBatch_PublishesNothingAndReportsZeroCounts()
    {
        var store = new FakeOutboxStore();
        store.BatchesToReturn.Enqueue([]);
        var publisher = new FakeServiceBusPublisher();
        var relay = CreateRelay(store, publisher);

        var result = await relay.RelayPendingAsync(CreateOptions());

        Assert.Equal(0, result.ClaimedCount);
        Assert.Equal(0, result.DispatchedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(publisher.PublishedMessages);
        Assert.Empty(store.DispatchedMessageIds);
    }

    [Fact]
    public async Task RelayPendingAsync_SuccessfulSend_MarksDispatched()
    {
        var message = CreateMessage();
        var store = new FakeOutboxStore();
        store.BatchesToReturn.Enqueue([message]);
        var publisher = new FakeServiceBusPublisher();
        var relay = CreateRelay(store, publisher);

        var result = await relay.RelayPendingAsync(CreateOptions());

        Assert.Equal(1, result.ClaimedCount);
        Assert.Equal(1, result.DispatchedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Single(publisher.PublishedMessages);
        Assert.Equal(message.Id.ToString(), publisher.PublishedMessages[0].MessageId);
        Assert.Equal(message.Payload, publisher.PublishedMessages[0].Body);
        Assert.Equal([message.Id], store.DispatchedMessageIds);
        Assert.Empty(store.FailedAttempts);
    }

    [Fact]
    public async Task RelayPendingAsync_FailedSend_RemainsPending_AndIsNotMarkedDispatched()
    {
        var message = CreateMessage();
        var store = new FakeOutboxStore();
        store.BatchesToReturn.Enqueue([message]);
        var publisher = new FakeServiceBusPublisher();
        publisher.MessageIdsThatFail.Add(message.Id.ToString());
        var relay = CreateRelay(store, publisher);

        var result = await relay.RelayPendingAsync(CreateOptions());

        Assert.Equal(1, result.ClaimedCount);
        Assert.Equal(0, result.DispatchedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Empty(store.DispatchedMessageIds);
        Assert.Single(store.FailedAttempts);
        Assert.Equal(message.Id, store.FailedAttempts[0].MessageId);
        Assert.Contains(message.Id.ToString(), store.FailedAttempts[0].Error);
    }

    [Fact]
    public async Task RelayPendingAsync_PartialBatch_ProcessesEachMessageIndependently()
    {
        var succeeding1 = CreateMessage();
        var failing = CreateMessage();
        var succeeding2 = CreateMessage();
        var store = new FakeOutboxStore();
        store.BatchesToReturn.Enqueue([succeeding1, failing, succeeding2]);
        var publisher = new FakeServiceBusPublisher();
        publisher.MessageIdsThatFail.Add(failing.Id.ToString());
        var relay = CreateRelay(store, publisher);

        var result = await relay.RelayPendingAsync(CreateOptions());

        Assert.Equal(3, result.ClaimedCount);
        Assert.Equal(2, result.DispatchedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(2, store.DispatchedMessageIds.Count);
        Assert.Contains(succeeding1.Id, store.DispatchedMessageIds);
        Assert.Contains(succeeding2.Id, store.DispatchedMessageIds);
        Assert.Single(store.FailedAttempts);
        Assert.Equal(failing.Id, store.FailedAttempts[0].MessageId);
    }

    [Fact]
    public async Task RelayPendingAsync_CancelledBeforeDispatch_ThrowsAndDoesNotRecordFailedAttempt()
    {
        var first = CreateMessage();
        var second = CreateMessage();
        var store = new FakeOutboxStore();
        store.BatchesToReturn.Enqueue([first, second]);
        var publisher = new FakeServiceBusPublisher();
        var relay = CreateRelay(store, publisher);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => relay.RelayPendingAsync(CreateOptions(), cts.Token));

        Assert.Empty(publisher.PublishedMessages);
        Assert.Empty(store.DispatchedMessageIds);
        Assert.Empty(store.FailedAttempts);
    }

    [Fact]
    public async Task RelayPendingAsync_CancelledMidBatch_StopsProcessingRemainingMessages()
    {
        var first = CreateMessage();
        var second = CreateMessage();
        var store = new FakeOutboxStore();
        store.BatchesToReturn.Enqueue([first, second]);

        using var cts = new CancellationTokenSource();
        var publisher = new CancelAfterFirstPublishPublisher(cts);
        var relay = CreateRelay(store, publisher);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => relay.RelayPendingAsync(CreateOptions(), cts.Token));

        // Only the first message was published/dispatched before cancellation stopped the loop.
        Assert.Single(publisher.PublishedMessages);
        Assert.Equal([first.Id], store.DispatchedMessageIds);
        Assert.Empty(store.FailedAttempts);
    }

    private sealed class CancelAfterFirstPublishPublisher(CancellationTokenSource cts) : IServiceBusPublisher
    {
        public List<OutboundServiceBusMessage> PublishedMessages { get; } = [];

        public Task PublishAsync(string entityName, OutboundServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            PublishedMessages.Add(message);
            if (PublishedMessages.Count == 1)
            {
                cts.Cancel();
            }

            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RelayPendingAsync_PassesBatchSizeLeaseOwnerAndLeaseDuration_ToTheStoreUnchanged()
    {
        var store = new FakeOutboxStore();
        store.BatchesToReturn.Enqueue([]);
        var publisher = new FakeServiceBusPublisher();
        var relay = CreateRelay(store, publisher);
        var options = CreateOptions();

        await relay.RelayPendingAsync(options);

        Assert.Equal(options.BatchSize, store.BatchSizeRequested);
        Assert.Equal(options.LeaseOwner, store.LeaseOwnerRequested);
        Assert.Equal(options.LeaseDuration, store.LeaseDurationRequested);
    }

    [Fact]
    public async Task RelayPendingAsync_TrustsWhateverTheStoreClaims_WithoutReselecting()
    {
        // The store is the sole source of lease/concurrency safety (messaging.md); the relay must
        // process exactly what ClaimPendingBatchAsync returned, even though it requested a larger
        // batch size, and must not call the store's claim method more than once per run.
        var claimed = new[] { CreateMessage(), CreateMessage() };
        var store = new FakeOutboxStore();
        store.BatchesToReturn.Enqueue(claimed);
        var publisher = new FakeServiceBusPublisher();
        var relay = CreateRelay(store, publisher);

        var result = await relay.RelayPendingAsync(CreateOptions() with { BatchSize = 100 });

        Assert.Equal(2, result.ClaimedCount);
        Assert.Equal(2, publisher.PublishedMessages.Count);
    }

    [Fact]
    public async Task RelayPendingAsync_EmitsDispatchedAndFailedCounters()
    {
        var succeeding = CreateMessage();
        var failing = CreateMessage();
        var store = new FakeOutboxStore();
        store.BatchesToReturn.Enqueue([succeeding, failing]);
        var publisher = new FakeServiceBusPublisher();
        publisher.MessageIdsThatFail.Add(failing.Id.ToString());
        var relay = CreateRelay(store, publisher);

        var measurements = new List<(string Instrument, long Value)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "ProjectChicago.Shared.Outbox.Relay")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
            measurements.Add((instrument.Name, value)));
        listener.Start();

        await relay.RelayPendingAsync(CreateOptions());

        Assert.Contains(measurements, m => m.Instrument == "outbox.relay.claimed" && m.Value == 2);
        Assert.Contains(measurements, m => m.Instrument == "outbox.relay.dispatched" && m.Value == 1);
        Assert.Contains(measurements, m => m.Instrument == "outbox.relay.failed" && m.Value == 1);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        var store = new FakeOutboxStore();
        var publisher = new FakeServiceBusPublisher();
        var logger = NullLogger<OutboxRelay>.Instance;

        Assert.Throws<ArgumentNullException>(() => new OutboxRelay(null!, publisher, logger));
        Assert.Throws<ArgumentNullException>(() => new OutboxRelay(store, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new OutboxRelay(store, publisher, null!));
    }

    [Fact]
    public async Task RelayPendingAsync_NullOptions_Throws()
    {
        var relay = CreateRelay(new FakeOutboxStore(), new FakeServiceBusPublisher());

        await Assert.ThrowsAsync<ArgumentNullException>(() => relay.RelayPendingAsync(null!));
    }
}
