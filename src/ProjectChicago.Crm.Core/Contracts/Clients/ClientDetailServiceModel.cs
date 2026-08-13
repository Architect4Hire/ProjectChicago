using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Clients;

// Business-owned output of the Client detail query, and the future public response contract for
// GET api/clients/{clientId} (CLIENT-030..032; the route itself is not implemented by this
// microstep - RESTRICTION: no controller/UI). ClientBusiness builds this from
// ClientDetailQueryResult - no Controller/Facade code maps into or out of it;
// ClientContractMappingExtensions.ToClientDetailServiceModel is the only place that translation
// happens.
//
// Audit history and recent activity, both listed in CLIENT-030, are intentionally absent here:
// this microstep is constrained to CrmDb-only data (SCOPE), and ADR-0016 places audit reads behind
// the not-yet-built Audit Service's own HTTP API rather than direct/shared database access
// (CLAUDE.md Constraints: "A service may not read another service's database"). Those two
// sections are deferred to a later microstep that calls the Audit Service, not silently included
// or silently promised here.
public sealed record ClientDetailServiceModel
{
    [JsonPropertyName("client")]
    public required ClientServiceModel Client { get; init; }

    [JsonPropertyName("activeProjects")]
    public required IReadOnlyList<ClientProjectSummary> ActiveProjects { get; init; }

    [JsonPropertyName("historicalProjects")]
    public required IReadOnlyList<ClientProjectSummary> HistoricalProjects { get; init; }

    [JsonPropertyName("openTasks")]
    public required IReadOnlyList<ClientTaskSummary> OpenTasks { get; init; }

    [JsonPropertyName("recentlyCompletedTasks")]
    public required IReadOnlyList<ClientTaskSummary> RecentlyCompletedTasks { get; init; }
}
