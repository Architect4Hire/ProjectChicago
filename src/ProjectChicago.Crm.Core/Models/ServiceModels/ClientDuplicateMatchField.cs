namespace ProjectChicago.Crm.Core.Models.ServiceModels;

// Core-owned mirror of ProjectChicago.Crm.Contracts.Clients.ClientDuplicateMatchField (CLIENT-004).
// Kept separate so Business never depends on the API host's transport contracts (reference
// direction: the HTTP host references .Core, not the reverse) - a future Facade/mapping microstep
// translates this into the wire contract.
public enum ClientDuplicateMatchField
{
    Name = 0,
    PrimaryEmail = 1,
    PrimaryPhone = 2,
}
