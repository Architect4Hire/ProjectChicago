using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;

namespace ProjectChicago.Shared.Messaging;

// SDK implementation of IServiceBusPublisher. Takes an injected ServiceBusClient (registered by the
// owning service's composition root through Aspire - this class does not construct a client from a
// connection string or any hardcoded endpoint). Senders are cached per entity name and reused for
// the lifetime of this publisher, per the SDK's own sender-caching guidance.
public sealed class AzureServiceBusPublisher : IServiceBusPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public AzureServiceBusPublisher(ServiceBusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task PublishAsync(string entityName, OutboundServiceBusMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentNullException.ThrowIfNull(message);

        var sender = _senders.GetOrAdd(entityName, _client.CreateSender);

        await sender.SendMessageAsync(BuildMessage(message), cancellationToken).ConfigureAwait(false);
    }

    // MessageId and CorrelationId use the SDK's native properties (dedup key and the broker's own
    // correlation field, respectively). CausationId has no native equivalent, so it - along with
    // ContractType/ContractVersion/TraceId for consumer-side observability without body
    // deserialization (functions.md) - goes in ApplicationProperties. Subject carries ContractType
    // too, for tooling that surfaces the message label.
    private static ServiceBusMessage BuildMessage(OutboundServiceBusMessage message)
    {
        var sbMessage = new ServiceBusMessage(message.Body)
        {
            MessageId = message.MessageId,
            CorrelationId = message.CorrelationId,
            ContentType = "application/json",
            Subject = message.ContractType,
        };

        sbMessage.ApplicationProperties["ContractType"] = message.ContractType;
        sbMessage.ApplicationProperties["ContractVersion"] = message.ContractVersion;
        sbMessage.ApplicationProperties["TraceId"] = message.TraceId;

        if (message.CausationId is not null)
        {
            sbMessage.ApplicationProperties["CausationId"] = message.CausationId;
        }

        return sbMessage;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync().ConfigureAwait(false);
        }
    }
}
