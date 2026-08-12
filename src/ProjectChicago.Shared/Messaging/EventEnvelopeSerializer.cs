using System.Text.Json;

namespace ProjectChicago.Shared.Messaging;

// Deterministic JSON serialization for EventEnvelope<TPayload>, used to fill OutboxMessage.Payload
// and the message body sent to/read from Service Bus. Deserialize peeks ContractType/ContractVersion
// before binding Payload to TPayload, so an unsupported version is rejected deterministically
// without attempting - and potentially misinterpreting - a mismatched payload shape.
public static class EventEnvelopeSerializer
{
    // ASP.NET Core's Web defaults (camelCase property names, case-insensitive read) match the
    // documented envelope shape in the integration event catalog and keep the async wire format
    // consistent with the rest of the system's JSON conventions. WriteIndented stays false and no
    // properties are conditionally omitted, so the same envelope always serializes to the same
    // bytes.
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public static string Serialize<TPayload>(EventEnvelope<TPayload> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return JsonSerializer.Serialize(envelope, Options);
    }

    public static EventEnvelope<TPayload> Deserialize<TPayload>(string json, IReadOnlyCollection<int> supportedVersions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(supportedVersions);

        var header = ParseHeader(json);

        if (!supportedVersions.Contains(header.ContractVersion))
        {
            throw new UnsupportedContractVersionException(header.ContractType, header.ContractVersion, supportedVersions);
        }

        EventEnvelope<TPayload>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelope<TPayload>>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new EnvelopeDeserializationException(
                $"Envelope payload could not be bound to {typeof(TPayload).Name}: {ex.Message}", ex);
        }

        return envelope ?? throw new EnvelopeDeserializationException(
            "Envelope JSON deserialized to null.",
            new JsonException("Deserialize returned null for a non-nullable envelope."));
    }

    private static EnvelopeHeader ParseHeader(string json)
    {
        EnvelopeHeader? header;
        try
        {
            header = JsonSerializer.Deserialize<EnvelopeHeader>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new EnvelopeDeserializationException($"Envelope JSON could not be parsed: {ex.Message}", ex);
        }

        return header ?? throw new EnvelopeDeserializationException(
            "Envelope JSON deserialized to null.",
            new JsonException("Deserialize returned null for a non-nullable envelope header."));
    }

    // Reads only the two fields needed to decide version support, ignoring everything else
    // (including a Payload subtree that may not match any known TPayload) so an unsupported/unknown
    // contract can be rejected before any attempt to bind it to a specific CLR type.
    private sealed record EnvelopeHeader
    {
        public required string ContractType { get; init; }

        public required int ContractVersion { get; init; }
    }
}
