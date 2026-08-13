using System.Text.Json.Serialization;
using ProjectChicago.Crm.Contracts.Clients;

namespace ProjectChicago.Crm.Contracts.Projects;

// Business-owned output of Project creation, and the public response contract for POST /api/clients/{clientId}/projects
// returned as 201 Created (API-003/API-004; onion-boundaries.md: "Business owns ... translation
// between Facade and Data models"). ProjectBusiness builds this directly from the persisted Project
// aggregate - it is never the EF Project entity itself (api-contracts.md; backend.md), and no
// Controller/Facade code maps into or out of it; ProjectContractMappingExtensions.ToServiceModel is
// the only place that translation happens.
//
// ConcurrencyToken carries the Project's optimistic-concurrency value (DATA-008; mirrors
// Project.RowVersion) opaquely as an ASCII/base64 string, not a raw byte array, so REST clients can
// round-trip it (e.g. as a future PUT/PATCH If-Match header or request-body token) without a
// binary-encoding decision baked into this contract now.
public sealed record ProjectServiceModel
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("clientId")]
    public required Guid ClientId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("status")]
    public required ProjectStatusContract Status { get; init; }

    [JsonPropertyName("priority")]
    public required ProjectPriorityContract Priority { get; init; }

    [JsonPropertyName("ownerUserId")]
    public required string OwnerUserId { get; init; }

    [JsonPropertyName("startDateUtc")]
    public DateTime? StartDateUtc { get; init; }

    [JsonPropertyName("targetCompletionDateUtc")]
    public DateTime? TargetCompletionDateUtc { get; init; }

    [JsonPropertyName("actualCompletionDateUtc")]
    public DateTime? ActualCompletionDateUtc { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("createdAtUtc")]
    public required DateTime CreatedAtUtc { get; init; }

    [JsonPropertyName("createdBy")]
    public required string CreatedBy { get; init; }

    [JsonPropertyName("lastModifiedAtUtc")]
    public required DateTime LastModifiedAtUtc { get; init; }

    [JsonPropertyName("lastModifiedBy")]
    public required string LastModifiedBy { get; init; }

    [JsonPropertyName("concurrencyToken")]
    public required string ConcurrencyToken { get; init; }
}
