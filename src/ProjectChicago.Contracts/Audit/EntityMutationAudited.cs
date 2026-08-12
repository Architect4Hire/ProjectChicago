namespace ProjectChicago.Contracts.Audit;

// The single cross-service audit fact every owning service publishes through its transactional
// outbox for a Client/Project/Task mutation (ADR-0016, AUDIT-001). Audit consumes this one
// generic contract rather than every owning service's per-entity business events, so it carries
// exactly the minimum durable data AUDIT-002 requires - no EF entities, credentials, tokens,
// full customer payloads, repositories, Service Bus SDK types, or AuditDb storage fields
// (AuditEntryId/AuditedAt/ActionCategory/RawEventPayload/SummaryDescription live only in
// Audit's own internal AuditEntry model, not on this wire contract).
public sealed record EntityMutationAudited
{
    public const int CurrentVersion = 1;

    // Used for outbox publish and Audit inbox idempotency (ASYNC-005, OUTBOX-004).
    public required string EventId { get; init; }

    public int Version { get; init; } = CurrentVersion;

    public required DateTimeOffset OccurredAtUtc { get; init; }

    // See AuditSourceServices.
    public required string SourceService { get; init; }

    // See AuditEntityTypes.
    public required string EntityType { get; init; }

    public required Guid EntityId { get; init; }

    // See AuditActions for the common, non-exhaustive vocabulary (AUDIT-003).
    public required string Action { get; init; }

    // Null for system/service-initiated mutations with no human actor.
    public string? ActorId { get; init; }

    // See AuditActorTypes.
    public required string ActorType { get; init; }

    public required string TraceId { get; init; }

    public required string CorrelationId { get; init; }

    public string? CausationId { get; init; }

    // Field names only, never values - always safe to include.
    public IReadOnlyList<string> ChangedFields { get; init; } = [];

    // Only fields approved as safe to disclose (AUDIT-008, PRIV-002). Omit a field entirely
    // rather than including an unsafe value; see AuditSensitiveFieldNames.
    public IReadOnlyDictionary<string, string>? PreviousValues { get; init; }

    public IReadOnlyDictionary<string, string>? NewValues { get; init; }

    // The compiler-synthesized record equality compares ChangedFields/PreviousValues/NewValues
    // by reference (List<T>/Dictionary<TKey,TValue> don't override Equals), so two payloads with
    // identical content - e.g. before and after a JSON round-trip - would otherwise compare
    // unequal. Compare structurally instead.
    public bool Equals(EntityMutationAudited? other) =>
        other is not null
        && (ReferenceEquals(this, other)
            || (EventId == other.EventId
                && Version == other.Version
                && OccurredAtUtc == other.OccurredAtUtc
                && SourceService == other.SourceService
                && EntityType == other.EntityType
                && EntityId == other.EntityId
                && Action == other.Action
                && ActorId == other.ActorId
                && ActorType == other.ActorType
                && TraceId == other.TraceId
                && CorrelationId == other.CorrelationId
                && CausationId == other.CausationId
                && ChangedFields.SequenceEqual(other.ChangedFields)
                && ValuesEqual(PreviousValues, other.PreviousValues)
                && ValuesEqual(NewValues, other.NewValues)));

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(EventId);
        hash.Add(Version);
        hash.Add(OccurredAtUtc);
        hash.Add(SourceService);
        hash.Add(EntityType);
        hash.Add(EntityId);
        hash.Add(Action);
        hash.Add(ActorId);
        hash.Add(ActorType);
        hash.Add(TraceId);
        hash.Add(CorrelationId);
        hash.Add(CausationId);
        foreach (var field in ChangedFields)
        {
            hash.Add(field);
        }

        return hash.ToHashCode();
    }

    private static bool ValuesEqual(IReadOnlyDictionary<string, string>? left, IReadOnlyDictionary<string, string>? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Count == right.Count
            && left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }
}
