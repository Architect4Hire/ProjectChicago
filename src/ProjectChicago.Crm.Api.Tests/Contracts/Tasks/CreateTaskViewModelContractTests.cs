using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ProjectChicago.Crm.Contracts.Tasks;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests.Contracts.Tasks;

// Locks the POST /api/projects/{projectId}/tasks request wire shape (TASK-001..002, API-001..007) and its
// transport-level shape/format validation (SEC-022) independently of any future controller/MVC
// JSON configuration - every property carries an explicit [JsonPropertyName], so these
// expectations hold under a plain System.Text.Json.JsonSerializer.Serialize/Deserialize call.
public class CreateTaskViewModelContractTests
{
    [Fact]
    public void Serialize_FullRequest_UsesCamelCasePropertyNamesAndStringStatusPriority()
    {
        var request = new CreateTaskViewModel
        {
            ProjectId = Guid.NewGuid(),
            Title = "Implement user authentication",
            Description = "Add ASP.NET Core Identity integration",
            Status = TaskItemStatusContract.ToDo,
            Priority = TaskItemPriorityContract.High,
            AssignedUserId = "user-42",
            StartDateUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDateUtc = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            Notes = "Coordinate with security team",
        };

        var json = JsonSerializer.Serialize(request);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.NotNull(root.GetProperty("projectId").GetString());
        Assert.Equal("Implement user authentication", root.GetProperty("title").GetString());
        Assert.Equal("Add ASP.NET Core Identity integration", root.GetProperty("description").GetString());
        Assert.Equal("ToDo", root.GetProperty("status").GetString());
        Assert.Equal("High", root.GetProperty("priority").GetString());
        Assert.Equal("user-42", root.GetProperty("assignedUserId").GetString());
        Assert.Equal("Coordinate with security team", root.GetProperty("notes").GetString());
    }

    [Fact]
    public void Deserialize_MinimalPayload_LeavesOptionalFieldsNull()
    {
        var projectId = Guid.NewGuid();
        var json = $$"""
            {
                "projectId": "{{projectId}}",
                "title": "Implement user authentication"
            }
            """;

        var request = JsonSerializer.Deserialize<CreateTaskViewModel>(json);

        Assert.NotNull(request);
        Assert.Equal(projectId, request!.ProjectId);
        Assert.Equal("Implement user authentication", request.Title);
        Assert.Null(request.Description);
        Assert.Null(request.Status);
        Assert.Null(request.Priority);
        Assert.Null(request.AssignedUserId);
        Assert.Null(request.StartDateUtc);
        Assert.Null(request.DueDateUtc);
        Assert.Null(request.Notes);
    }

    [Fact]
    public void Deserialize_MissingRequiredProjectId_ThrowsBeforeAnyValidatorRuns()
    {
        const string json = """
            {
                "title": "Implement user authentication"
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateTaskViewModel>(json));
    }

    [Fact]
    public void Deserialize_MissingRequiredTitle_ThrowsBeforeAnyValidatorRuns()
    {
        var projectId = Guid.NewGuid();
        var json = $$"""
            {
                "projectId": "{{projectId}}"
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateTaskViewModel>(json));
    }

    [Fact]
    public void Validate_FullyPopulatedValidRequest_ProducesNoValidationErrors()
    {
        var request = new CreateTaskViewModel
        {
            ProjectId = Guid.NewGuid(),
            Title = "Implement user authentication",
            Status = TaskItemStatusContract.InProgress,
            Priority = TaskItemPriorityContract.Normal,
        };

        var errors = Validate(request);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankTitle_IsRejected(string blankTitle)
    {
        var request = new CreateTaskViewModel
        {
            ProjectId = Guid.NewGuid(),
            Title = blankTitle,
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateTaskViewModel.Title)));
    }

    [Fact]
    public void Validate_TitleLongerThanTwoHundredCharacters_IsRejected()
    {
        var request = new CreateTaskViewModel
        {
            ProjectId = Guid.NewGuid(),
            Title = new string('a', 201),
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateTaskViewModel.Title)));
    }

    [Fact]
    public void Validate_DescriptionLongerThanTwoThousandCharacters_IsRejected()
    {
        var request = new CreateTaskViewModel
        {
            ProjectId = Guid.NewGuid(),
            Title = "Implement user authentication",
            Description = new string('a', 2001),
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateTaskViewModel.Description)));
    }

    [Fact]
    public void Validate_AssignedUserIdLongerThanOneHundredTwentyEightCharacters_IsRejected()
    {
        var request = new CreateTaskViewModel
        {
            ProjectId = Guid.NewGuid(),
            Title = "Implement user authentication",
            AssignedUserId = new string('a', 129),
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateTaskViewModel.AssignedUserId)));
    }

    [Fact]
    public void Validate_NotesLongerThanTwoThousandCharacters_IsRejected()
    {
        var request = new CreateTaskViewModel
        {
            ProjectId = Guid.NewGuid(),
            Title = "Implement user authentication",
            Notes = new string('a', 2001),
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateTaskViewModel.Notes)));
    }

    private static IReadOnlyList<ValidationResult> Validate(CreateTaskViewModel request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
