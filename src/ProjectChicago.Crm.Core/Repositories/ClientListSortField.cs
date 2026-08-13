namespace ProjectChicago.Crm.Core.Repositories;

// Repository-level mirror of ProjectChicago.Crm.Contracts.Clients.ClientSortField (CLIENT-023).
// Kept separate from the wire contract so IClientRepository never depends on API contract types
// (data.md: "Do not reference controllers, API contracts, ..."; onion-boundaries.md: translation
// between transport and persistence-facing models is a Business-layer concern, not this seam's).
public enum ClientListSortField
{
    Name = 0,
    CreatedAtUtc = 1,
    LastModifiedAtUtc = 2,
    LifecycleStatus = 3,
}
