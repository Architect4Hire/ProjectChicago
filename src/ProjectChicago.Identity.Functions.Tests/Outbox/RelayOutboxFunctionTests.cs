using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProjectChicago.Identity.Functions.Outbox;
using ProjectChicago.Shared.Messaging;
using Xunit;

namespace ProjectChicago.Identity.Functions.Tests.Outbox;

public class RelayOutboxFunctionTests
{
    private sealed class FakeOutboxRelay : IOutboxRelay
    {
        public int CallCount { get; private set; }

        public List<OutboxRelayOptions> ReceivedOptions { get; } = [];

        public CancellationToken? LastCancellationToken { get; private set; }

        public Exception? ExceptionToThrow { get; set; }

        public OutboxRelayResult ResultToReturn { get; set; } =
            new() { ClaimedCount = 0, DispatchedCount = 0, FailedCount = 0 };

        public Task<OutboxRelayResult> RelayPendingAsync(OutboxRelayOptions options, CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedOptions.Add(options);
            LastCancellationToken = cancellationToken;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(ResultToReturn);
        }
    }

    private static OutboxRelaySettings CreateSettings() => new()
    {
        EntityName = "ProjectChicago.Events",
        BatchSize = 25,
        LeaseDuration = TimeSpan.FromMinutes(1),
    };

    private static RelayOutboxFunction CreateFunction(FakeOutboxRelay relay, OutboxRelaySettings? settings = null) =>
        new(relay, Options.Create(settings ?? CreateSettings()), NullLogger<RelayOutboxFunction>.Instance);

    [Fact]
    public async Task RunAsync_DelegatesExactlyOnceToTheRelay_WithConfiguredOptions()
    {
        var relay = new FakeOutboxRelay();
        var settings = CreateSettings();
        var function = CreateFunction(relay, settings);

        await function.RunAsync(new TimerInfo(), CancellationToken.None);

        Assert.Equal(1, relay.CallCount);
        var options = Assert.Single(relay.ReceivedOptions);
        Assert.Equal(settings.EntityName, options.EntityName);
        Assert.Equal(settings.BatchSize, options.BatchSize);
        Assert.Equal(settings.LeaseDuration, options.LeaseDuration);
        Assert.False(string.IsNullOrWhiteSpace(options.LeaseOwner));
    }

    [Fact]
    public async Task RunAsync_GeneratesADistinctLeaseOwner_PerInvocation()
    {
        var relay = new FakeOutboxRelay();
        var function = CreateFunction(relay);

        await function.RunAsync(new TimerInfo(), CancellationToken.None);
        await function.RunAsync(new TimerInfo(), CancellationToken.None);

        Assert.Equal(2, relay.CallCount);
        Assert.NotEqual(relay.ReceivedOptions[0].LeaseOwner, relay.ReceivedOptions[1].LeaseOwner);
    }

    [Fact]
    public async Task RunAsync_PropagatesTheCancellationToken_ToTheRelayCallUnchanged()
    {
        var relay = new FakeOutboxRelay();
        var function = CreateFunction(relay);
        using var cts = new CancellationTokenSource();

        await function.RunAsync(new TimerInfo(), cts.Token);

        Assert.Equal(cts.Token, relay.LastCancellationToken);
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelledToken_ThrowsAndStillReachedTheRelayExactlyOnce()
    {
        var relay = new FakeOutboxRelay();
        var function = CreateFunction(relay);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => function.RunAsync(new TimerInfo(), cts.Token));

        Assert.Equal(1, relay.CallCount);
    }

    [Fact]
    public async Task RunAsync_RelayThrows_ExceptionPropagatesAndIsNotSwallowed()
    {
        var relay = new FakeOutboxRelay { ExceptionToThrow = new InvalidOperationException("simulated relay failure") };
        var function = CreateFunction(relay);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => function.RunAsync(new TimerInfo(), CancellationToken.None));

        Assert.Equal("simulated relay failure", ex.Message);
        Assert.Equal(1, relay.CallCount);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        var relay = new FakeOutboxRelay();
        var options = Options.Create(CreateSettings());
        var logger = NullLogger<RelayOutboxFunction>.Instance;

        Assert.Throws<ArgumentNullException>(() => new RelayOutboxFunction(null!, options, logger));
        Assert.Throws<ArgumentNullException>(() => new RelayOutboxFunction(relay, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new RelayOutboxFunction(relay, options, null!));
    }
}
