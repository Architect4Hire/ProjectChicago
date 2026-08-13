using System.Text.Json;
using ProjectChicago.Crm.Contracts.Projects;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests.Contracts.Projects;

// Locks the 201 Created response wire shape for POST /api/clients/{clientId}/projects (PROJECT-001..002, API-001..007,
// DATA-006, DATA-008) independently of any future controller/MVC JSON configuration.
public class ProjectServiceModelContractTests
{
    [Fact]
    public void Serialize_Response_RoundTripsThroughJsonPreservingAllFields()
    {
        var clientId = Guid.NewGuid();
        var createdAtUtc = new DateTime(2026, 9, 1, 10, 30, 0, DateTimeKind.Utc);
        var lastModifiedAtUtc = createdAtUtc;
        var startDateUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var targetCompletionDateUtc = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var response = new ProjectServiceModel
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Website Redesign",
            Description = "Complete redesign of company website",
            Status = ProjectStatusContract.Planned,
            Priority = ProjectPriorityContract.High,
            OwnerUserId = "user-42",
            StartDateUtc = startDateUtc,
            TargetCompletionDateUtc = targetCompletionDateUtc,
            ActualCompletionDateUtc = null,
            Notes = "Coordinate with design team",
            CreatedAtUtc = createdAtUtc,
            CreatedBy = "user-42",
            LastModifiedAtUtc = lastModifiedAtUtc,
            LastModifiedBy = "user-42",
            ConcurrencyToken = "AAAAAAAAB9E=",
        };

        var json = JsonSerializer.Serialize(response);
        var roundTripped = JsonSerializer.Deserialize<ProjectServiceModel>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(response.Id, roundTripped!.Id);
        Assert.Equal(response.ClientId, roundTripped.ClientId);
        Assert.Equal(response.Name, roundTripped.Name);
        Assert.Equal(response.Description, roundTripped.Description);
        Assert.Equal(response.Status, roundTripped.Status);
        Assert.Equal(response.Priority, roundTripped.Priority);
        Assert.Equal(response.OwnerUserId, roundTripped.OwnerUserId);
        Assert.Equal(response.StartDateUtc, roundTripped.StartDateUtc);
        Assert.Equal(response.TargetCompletionDateUtc, roundTripped.TargetCompletionDateUtc);
        Assert.Equal(response.ActualCompletionDateUtc, roundTripped.ActualCompletionDateUtc);
        Assert.Equal(response.Notes, roundTripped.Notes);
        Assert.Equal(response.CreatedAtUtc, roundTripped.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, roundTripped.CreatedAtUtc.Kind);
        Assert.Equal(response.ConcurrencyToken, roundTripped.ConcurrencyToken);
    }

    [Fact]
    public void Serialize_Response_UsesCamelCasePropertyNamesAndStringEnumsNotNumbers()
    {
        var clientId = Guid.NewGuid();
        var response = new ProjectServiceModel
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Website Redesign",
            Status = ProjectStatusContract.Active,
            Priority = ProjectPriorityContract.High,
            OwnerUserId = "user-42",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "user-42",
            LastModifiedAtUtc = DateTime.UtcNow,
            LastModifiedBy = "user-42",
            ConcurrencyToken = "AAAAAAAAB9E=",
        };

        var json = JsonSerializer.Serialize(response);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.True(root.TryGetProperty("id", out _));
        Assert.True(root.TryGetProperty("clientId", out _));
        Assert.True(root.TryGetProperty("name", out _));
        Assert.True(root.TryGetProperty("ownerUserId", out _));
        Assert.True(root.TryGetProperty("createdAtUtc", out _));
        Assert.True(root.TryGetProperty("concurrencyToken", out _));

        // Stable string enum serialization, not the numeric backing value (api-contracts.md).
        Assert.Equal("Active", root.GetProperty("status").GetString());
        Assert.Equal("High", root.GetProperty("priority").GetString());
    }

    [Fact]
    public void Serialize_ResponseWithActualCompletionDate_IncludesTheDate()
    {
        var clientId = Guid.NewGuid();
        var actualCompletionDateUtc = new DateTime(2026, 11, 15, 14, 30, 0, DateTimeKind.Utc);
        var response = new ProjectServiceModel
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Website Redesign",
            Status = ProjectStatusContract.Completed,
            Priority = ProjectPriorityContract.Normal,
            OwnerUserId = "user-42",
            ActualCompletionDateUtc = actualCompletionDateUtc,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "user-42",
            LastModifiedAtUtc = DateTime.UtcNow,
            LastModifiedBy = "user-42",
            ConcurrencyToken = "AAAAAAAAB9E=",
        };

        var json = JsonSerializer.Serialize(response);
        var roundTripped = JsonSerializer.Deserialize<ProjectServiceModel>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(actualCompletionDateUtc, roundTripped!.ActualCompletionDateUtc);
        Assert.Equal(DateTimeKind.Utc, roundTripped.ActualCompletionDateUtc!.Value.Kind);
    }

    [Fact]
    public void Serialize_ResponseWithoutOptionalDates_SerializesNullValues()
    {
        var clientId = Guid.NewGuid();
        var response = new ProjectServiceModel
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Website Redesign",
            Status = ProjectStatusContract.Planned,
            Priority = ProjectPriorityContract.Low,
            OwnerUserId = "user-42",
            StartDateUtc = null,
            TargetCompletionDateUtc = null,
            ActualCompletionDateUtc = null,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "user-42",
            LastModifiedAtUtc = DateTime.UtcNow,
            LastModifiedBy = "user-42",
            ConcurrencyToken = "AAAAAAAAB9E=",
        };

        var json = JsonSerializer.Serialize(response);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal(JsonValueKind.Null, root.GetProperty("startDateUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("targetCompletionDateUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("actualCompletionDateUtc").ValueKind);
    }

    [Fact]
    public void Serialize_DateTimesPreserveUtcKind()
    {
        var clientId = Guid.NewGuid();
        var createdAtUtc = new DateTime(2026, 9, 1, 10, 30, 0, DateTimeKind.Utc);
        var startDateUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        var response = new ProjectServiceModel
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Website Redesign",
            Status = ProjectStatusContract.Planned,
            Priority = ProjectPriorityContract.Normal,
            OwnerUserId = "user-42",
            StartDateUtc = startDateUtc,
            CreatedAtUtc = createdAtUtc,
            CreatedBy = "user-42",
            LastModifiedAtUtc = createdAtUtc,
            LastModifiedBy = "user-42",
            ConcurrencyToken = "AAAAAAAAB9E=",
        };

        var json = JsonSerializer.Serialize(response);
        var roundTripped = JsonSerializer.Deserialize<ProjectServiceModel>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(DateTimeKind.Utc, roundTripped!.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, roundTripped.StartDateUtc!.Value.Kind);
    }
}
