namespace ProjectChicago.Crm.Core.Models.ServiceModels;

// One likely-duplicate match Business found while creating a Client (CLIENT-004). Creation
// proceeds regardless - this rides alongside ClientCreationResult as a warning, never a block and
// never a silent merge.
public sealed record ClientDuplicateCandidate
{
    public required Guid ClientId { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<ClientDuplicateMatchField> MatchedOn { get; init; }
}
