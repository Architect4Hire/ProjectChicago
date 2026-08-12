using Azure.Messaging.ServiceBus;
using ProjectChicago.Shared.Messaging;
using Xunit;

namespace ProjectChicago.Shared.Tests;

// Uses the Azure SDK's own supported mocking pattern (parameterless ServiceBusClient/ServiceBusSender
// constructors + virtual CreateSender/SendMessageAsync) as the fake boundary, so these tests verify
// the message metadata AzureServiceBusPublisher builds without any network call or live namespace.
public class AzureServiceBusPublisherTests
{
    private sealed class FakeServiceBusSender : ServiceBusSender
    {
        public List<ServiceBusMessage> SentMessages { get; } = [];

        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeServiceBusClient : ServiceBusClient
    {
        public FakeServiceBusSender Sender { get; } = new();

        public int CreateSenderCallCount { get; private set; }

        public string? RequestedEntityName { get; private set; }

        public override ServiceBusSender CreateSender(string queueOrTopicName)
        {
            CreateSenderCallCount++;
            RequestedEntityName = queueOrTopicName;
            return Sender;
        }
    }

    private static OutboundServiceBusMessage CreateMessage() => new()
    {
        MessageId = "11111111-1111-1111-1111-111111111111",
        ContractType = "Audit.EntityMutationAudited",
        ContractVersion = 1,
        CorrelationId = "correlation-1",
        CausationId = "causation-1",
        TraceId = "4bf92f3577b34da6a3ce929d0e0e4736",
        Body = "{\"eventId\":\"event-1\"}",
    };

    [Fact]
    public async Task PublishAsync_SendsThroughSenderForTheRequestedEntity()
    {
        var client = new FakeServiceBusClient();
        var publisher = new AzureServiceBusPublisher(client);

        await publisher.PublishAsync("ProjectChicago.Events", CreateMessage());

        Assert.Equal("ProjectChicago.Events", client.RequestedEntityName);
        Assert.Single(client.Sender.SentMessages);
    }

    [Fact]
    public async Task PublishAsync_SetsMessageIdAndCorrelationId_AsNativeProperties()
    {
        var client = new FakeServiceBusClient();
        var publisher = new AzureServiceBusPublisher(client);
        var message = CreateMessage();

        await publisher.PublishAsync("ProjectChicago.Events", message);

        var sent = client.Sender.SentMessages.Single();
        Assert.Equal(message.MessageId, sent.MessageId);
        Assert.Equal(message.CorrelationId, sent.CorrelationId);
    }

    [Fact]
    public async Task PublishAsync_SetsBodyAndContentType()
    {
        var client = new FakeServiceBusClient();
        var publisher = new AzureServiceBusPublisher(client);
        var message = CreateMessage();

        await publisher.PublishAsync("ProjectChicago.Events", message);

        var sent = client.Sender.SentMessages.Single();
        Assert.Equal(message.Body, sent.Body.ToString());
        Assert.Equal("application/json", sent.ContentType);
    }

    [Fact]
    public async Task PublishAsync_SetsSubjectToContractType()
    {
        var client = new FakeServiceBusClient();
        var publisher = new AzureServiceBusPublisher(client);
        var message = CreateMessage();

        await publisher.PublishAsync("ProjectChicago.Events", message);

        Assert.Equal(message.ContractType, client.Sender.SentMessages.Single().Subject);
    }

    [Fact]
    public async Task PublishAsync_SetsContractAndTraceAndCausationApplicationProperties()
    {
        var client = new FakeServiceBusClient();
        var publisher = new AzureServiceBusPublisher(client);
        var message = CreateMessage();

        await publisher.PublishAsync("ProjectChicago.Events", message);

        var properties = client.Sender.SentMessages.Single().ApplicationProperties;
        Assert.Equal(message.ContractType, properties["ContractType"]);
        Assert.Equal(message.ContractVersion, properties["ContractVersion"]);
        Assert.Equal(message.TraceId, properties["TraceId"]);
        Assert.Equal(message.CausationId, properties["CausationId"]);
    }

    [Fact]
    public async Task PublishAsync_NullCausationId_OmitsCausationApplicationProperty()
    {
        var client = new FakeServiceBusClient();
        var publisher = new AzureServiceBusPublisher(client);
        var message = CreateMessage() with { CausationId = null };

        await publisher.PublishAsync("ProjectChicago.Events", message);

        Assert.False(client.Sender.SentMessages.Single().ApplicationProperties.ContainsKey("CausationId"));
    }

    [Fact]
    public async Task PublishAsync_ReusesCachedSenderForTheSameEntityName()
    {
        var client = new FakeServiceBusClient();
        var publisher = new AzureServiceBusPublisher(client);

        await publisher.PublishAsync("ProjectChicago.Events", CreateMessage());
        await publisher.PublishAsync("ProjectChicago.Events", CreateMessage());

        Assert.Equal(1, client.CreateSenderCallCount);
        Assert.Equal(2, client.Sender.SentMessages.Count);
    }

    [Fact]
    public async Task PublishAsync_NullOrWhitespaceEntityName_Throws()
    {
        var client = new FakeServiceBusClient();
        var publisher = new AzureServiceBusPublisher(client);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => publisher.PublishAsync(" ", CreateMessage()));
    }

    [Fact]
    public async Task PublishAsync_NullMessage_Throws()
    {
        var client = new FakeServiceBusClient();
        var publisher = new AzureServiceBusPublisher(client);

        await Assert.ThrowsAsync<ArgumentNullException>(() => publisher.PublishAsync("ProjectChicago.Events", null!));
    }

    [Fact]
    public void Constructor_NullClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AzureServiceBusPublisher(null!));
    }
}
