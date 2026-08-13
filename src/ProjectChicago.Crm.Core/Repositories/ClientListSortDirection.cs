namespace ProjectChicago.Crm.Core.Repositories;

// Repository-level mirror of ProjectChicago.Crm.Contracts.Clients.ClientSortDirection (CLIENT-023).
// Kept separate from the wire contract for the same reason as ClientListSortField.
public enum ClientListSortDirection
{
    Ascending = 0,
    Descending = 1,
}
