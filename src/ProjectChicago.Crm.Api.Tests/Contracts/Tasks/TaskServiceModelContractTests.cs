using System.Text.Json;
using ProjectChicago.Crm.Contracts.Tasks;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests.Contracts.Tasks;

// Locks the POST /api/projects/{projectId}/tasks response wire shape (TASK-001..002, API-003..007) and its
// serialization contract independently of any future MVC JSON configuration - every property carries an
// explicit [JsonPropertyName], so these expectations hold under a plain System.Text.Json.JsonSerializer.Serialize call.
public class TaskServiceModelContractTests
{
    [Fact]
    public void Serialize_FullResponse_UsesCamelCasePropertyNamesAndStringStatusPriority()
    {
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var createdAtUtc = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var startDateUtc = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);
        var dueDateUtc = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);

        var response = new TaskServiceModel
        {
            Id = taskId,
            ProjectId = projectId,
            Title = "Implement user authentication",
            Description = "Add ASP.NET Core Identity integration",
            Status = TaskItemStatusContract.InProgress,
            Priority = TaskItemPriorityContract.High,
            AssignedUserId = "user-42",
            StartDateUtc = startDateUtc,
            DueDateUtc = dueDateUtc,
            CompletedAtUtc = null,
            Notes = "Coordinate with security team",
            CreatedAtUtc = createdAtUtc,
            CreatedBy = "user-1",
            LastModifiedAtUtc = createdAtUtc,
            LastModifiedBy = "user-1",
            ConcurrencyToken = "AQIDBA==",
        };

        var json = JsonSerializer.Serialize(response);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal(taskId.ToString(), root.GetProperty("id").GetString());
        Assert.Equal(projectId.ToString(), root.GetProperty("projectId").GetString());
        Assert.Equal("Implement user authentication", root.GetProperty("title").GetString());
        Assert.Equal("Add ASP.NET Core Identity integration", root.GetProperty("description").GetString());
        Assert.Equal("InProgress", root.GetProperty("status").GetString());
        Assert.Equal("High", root.GetProperty("priority").GetString());
        Assert.Equal("user-42", root.GetProperty("assignedUserId").GetString());
        Assert.Equal("Coordinate with security team", root.GetProperty("notes").GetString());
        Assert.Equal("user-1", root.GetProperty("createdBy").GetString());
        Assert.Equal("AQIDBA==", root.GetProperty("concurrencyToken").GetString());
    }

    [Fact]
    public void Serialize_ResponseWithoutOptionalFields_SerializesNullFieldsAsJsonNull()
    {
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var createdAtUtc = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        var response = new TaskServiceModel
        {
            Id = taskId,
            ProjectId = projectId,
            Title = "Task without details",
            Description = null,
            Status = TaskItemStatusContract.Backlog,
            Priority = TaskItemPriorityContract.Normal,
            AssignedUserId = null,
            StartDateUtc = null,
            DueDateUtc = null,
            CompletedAtUtc = null,
            Notes = null,
            CreatedAtUtc = createdAtUtc,
            CreatedBy = "user-1",
            LastModifiedAtUtc = createdAtUtc,
            LastModifiedBy = "user-1",
            ConcurrencyToken = "AQIDBA==",
        };

        var json = JsonSerializer.Serialize(response);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.NotNull(root.GetProperty("id").GetString());
        Assert.NotNull(root.GetProperty("title").GetString());
        Assert.Equal("Task without details", root.GetProperty("title").GetString());
        // Null values are included in JSON serialization
        Assert.True(root.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.Null);
        Assert.True(root.TryGetProperty("assignedUserId", out var assignedUserId) && assignedUserId.ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public void Serialize_AllStatusValues_ProduceCorrectStringRepresentations()
    {
        var statuses = new[]
        {
            (TaskItemStatusContract.Backlog, "Backlog"),
            (TaskItemStatusContract.ToDo, "ToDo"),
            (TaskItemStatusContract.InProgress, "InProgress"),
            (TaskItemStatusContract.Blocked, "Blocked"),
            (TaskItemStatusContract.Completed, "Completed"),
            (TaskItemStatusContract.Cancelled, "Cancelled"),
        };

        foreach (var (status, expectedString) in statuses)
        {
            var response = new TaskServiceModel
            {
                Id = Guid.NewGuid(),
                ProjectId = Guid.NewGuid(),
                Title = "Test",
                Status = status,
                Priority = TaskItemPriorityContract.Normal,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "user-1",
                LastModifiedAtUtc = DateTime.UtcNow,
                LastModifiedBy = "user-1",
                ConcurrencyToken = "token",
            };

            var json = JsonSerializer.Serialize(response);
            var root = JsonDocument.Parse(json).RootElement;

            Assert.Equal(expectedString, root.GetProperty("status").GetString());
        }
    }

    [Fact]
    public void Serialize_AllPriorityValues_ProduceCorrectStringRepresentations()
    {
        var priorities = new[]
        {
            (TaskItemPriorityContract.Low, "Low"),
            (TaskItemPriorityContract.Normal, "Normal"),
            (TaskItemPriorityContract.High, "High"),
            (TaskItemPriorityContract.Critical, "Critical"),
        };

        foreach (var (priority, expectedString) in priorities)
        {
            var response = new TaskServiceModel
            {
                Id = Guid.NewGuid(),
                ProjectId = Guid.NewGuid(),
                Title = "Test",
                Status = TaskItemStatusContract.Backlog,
                Priority = priority,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "user-1",
                LastModifiedAtUtc = DateTime.UtcNow,
                LastModifiedBy = "user-1",
                ConcurrencyToken = "token",
            };

            var json = JsonSerializer.Serialize(response);
            var root = JsonDocument.Parse(json).RootElement;

            Assert.Equal(expectedString, root.GetProperty("priority").GetString());
        }
    }

    [Fact]
    public void Serialize_CompletedTask_IncludesCompletionTimestamp()
    {
        var completedAtUtc = new DateTime(2026, 9, 10, 14, 30, 0, DateTimeKind.Utc);

        var response = new TaskServiceModel
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Title = "Completed task",
            Status = TaskItemStatusContract.Completed,
            Priority = TaskItemPriorityContract.Normal,
            CompletedAtUtc = completedAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "user-1",
            LastModifiedAtUtc = DateTime.UtcNow,
            LastModifiedBy = "user-1",
            ConcurrencyToken = "token",
        };

        var json = JsonSerializer.Serialize(response);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal(completedAtUtc, root.GetProperty("completedAtUtc").GetDateTime());
    }

    [Fact]
    public void Serialize_CreatedAndModifiedByDifferentUsers_IncludeBothUserIds()
    {
        var createdAtUtc = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var lastModifiedAtUtc = new DateTime(2026, 9, 5, 15, 0, 0, DateTimeKind.Utc);

        var response = new TaskServiceModel
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Title = "Task modified after creation",
            Status = TaskItemStatusContract.InProgress,
            Priority = TaskItemPriorityContract.Normal,
            CreatedAtUtc = createdAtUtc,
            CreatedBy = "user-1",
            LastModifiedAtUtc = lastModifiedAtUtc,
            LastModifiedBy = "user-2",
            ConcurrencyToken = "token",
        };

        var json = JsonSerializer.Serialize(response);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal("user-1", root.GetProperty("createdBy").GetString());
        Assert.Equal("user-2", root.GetProperty("lastModifiedBy").GetString());
        Assert.NotEqual(createdAtUtc, lastModifiedAtUtc);
    }
}
