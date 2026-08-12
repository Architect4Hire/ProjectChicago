using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Models.ServiceModels;

// Business-layer output of Client creation (CLIENT-001..004). Carries the persisted Client plus any
// CLIENT-004 duplicate warnings so a future Facade can map both onto ClientResponse without a
// second round trip to Data.
public sealed record ClientCreationResult
{
    public required Client Client { get; init; }

    public IReadOnlyList<ClientDuplicateCandidate> PossibleDuplicates { get; init; } = [];
}
