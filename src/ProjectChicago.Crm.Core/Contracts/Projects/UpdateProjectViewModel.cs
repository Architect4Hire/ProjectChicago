using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectChicago.Crm.Contracts.Projects;

// Public PATCH /api/projects/{projectId} request contract for ordinary detail editing
// (PROJECT-002, DATA-008, AUDIT-001..008). Allows updating name, description, priority, owner,
// start/target dates, and notes. Does not allow changing Client ownership, Project status,
// actual completion date, or archive state - those remain dedicated operations.
//
// Field bounds mirror ProjectConfiguration's SQL Server column lengths.
// DataAnnotations catch only shape/format problems at the transport boundary (backend.md).
public sealed record UpdateProjectViewModel
{
    [JsonPropertyName("name")]
    [StringLength(200, MinimumLength = 1)]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    [StringLength(2000)]
    public string? Description { get; init; }

    [JsonPropertyName("priority")]
    [EnumDataType(typeof(ProjectPriorityContract))]
    public ProjectPriorityContract? Priority { get; init; }

    [JsonPropertyName("ownerUserId")]
    [StringLength(128, MinimumLength = 1)]
    public string? OwnerUserId { get; init; }

    [JsonPropertyName("startDateUtc")]
    public DateTime? StartDateUtc { get; init; }

    [JsonPropertyName("targetCompletionDateUtc")]
    public DateTime? TargetCompletionDateUtc { get; init; }

    [JsonPropertyName("notes")]
    [StringLength(2000)]
    public string? Notes { get; init; }
}
