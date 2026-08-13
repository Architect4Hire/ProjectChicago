using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ProjectChicago.Crm.Contracts.Projects;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests.Contracts.Projects;

// Locks the POST /api/clients/{clientId}/projects request wire shape (PROJECT-001..002, API-001..007) and its
// transport-level shape/format validation (SEC-022) independently of any future controller/MVC
// JSON configuration - every property carries an explicit [JsonPropertyName], so these
// expectations hold under a plain System.Text.Json.JsonSerializer.Serialize/Deserialize call.
public class CreateProjectViewModelContractTests
{
    [Fact]
    public void Serialize_FullRequest_UsesCamelCasePropertyNamesAndStringStatusPriority()
    {
        var request = new CreateProjectViewModel
        {
            ClientId = Guid.NewGuid(),
            Name = "Website Redesign",
            Description = "Complete redesign of company website",
            Status = ProjectStatusContract.Planned,
            Priority = ProjectPriorityContract.High,
            OwnerUserId = "user-42",
            StartDateUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            TargetCompletionDateUtc = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Notes = "Coordinate with design team",
        };

        var json = JsonSerializer.Serialize(request);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.NotNull(root.GetProperty("clientId").GetString());
        Assert.Equal("Website Redesign", root.GetProperty("name").GetString());
        Assert.Equal("Complete redesign of company website", root.GetProperty("description").GetString());
        Assert.Equal("Planned", root.GetProperty("status").GetString());
        Assert.Equal("High", root.GetProperty("priority").GetString());
        Assert.Equal("user-42", root.GetProperty("ownerUserId").GetString());
        Assert.Equal("Coordinate with design team", root.GetProperty("notes").GetString());
    }

    [Fact]
    public void Deserialize_MinimalPayload_LeavesOptionalFieldsNull()
    {
        var clientId = Guid.NewGuid();
        var json = $$"""
            {
                "clientId": "{{clientId}}",
                "name": "Website Redesign",
                "ownerUserId": "user-42"
            }
            """;

        var request = JsonSerializer.Deserialize<CreateProjectViewModel>(json);

        Assert.NotNull(request);
        Assert.Equal(clientId, request!.ClientId);
        Assert.Equal("Website Redesign", request.Name);
        Assert.Equal("user-42", request.OwnerUserId);
        Assert.Null(request.Description);
        Assert.Null(request.Status);
        Assert.Null(request.Priority);
        Assert.Null(request.StartDateUtc);
        Assert.Null(request.TargetCompletionDateUtc);
        Assert.Null(request.Notes);
    }

    [Fact]
    public void Deserialize_MissingRequiredClientId_ThrowsBeforeAnyValidatorRuns()
    {
        const string json = """
            {
                "name": "Website Redesign",
                "ownerUserId": "user-42"
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateProjectViewModel>(json));
    }

    [Fact]
    public void Deserialize_MissingRequiredName_ThrowsBeforeAnyValidatorRuns()
    {
        var clientId = Guid.NewGuid();
        var json = $$"""
            {
                "clientId": "{{clientId}}",
                "ownerUserId": "user-42"
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateProjectViewModel>(json));
    }

    [Fact]
    public void Deserialize_MissingRequiredOwnerUserId_ThrowsBeforeAnyValidatorRuns()
    {
        var clientId = Guid.NewGuid();
        var json = $$"""
            {
                "clientId": "{{clientId}}",
                "name": "Website Redesign"
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateProjectViewModel>(json));
    }

    [Fact]
    public void Validate_FullyPopulatedValidRequest_ProducesNoValidationErrors()
    {
        var request = new CreateProjectViewModel
        {
            ClientId = Guid.NewGuid(),
            Name = "Website Redesign",
            OwnerUserId = "user-42",
            Status = ProjectStatusContract.Active,
            Priority = ProjectPriorityContract.Normal,
        };

        var errors = Validate(request);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankName_IsRejected(string blankName)
    {
        var request = new CreateProjectViewModel
        {
            ClientId = Guid.NewGuid(),
            Name = blankName,
            OwnerUserId = "user-42",
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateProjectViewModel.Name)));
    }

    [Fact]
    public void Validate_NameLongerThanTwoHundredCharacters_IsRejected()
    {
        var request = new CreateProjectViewModel
        {
            ClientId = Guid.NewGuid(),
            Name = new string('a', 201),
            OwnerUserId = "user-42",
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateProjectViewModel.Name)));
    }

    [Fact]
    public void Validate_DescriptionLongerThanTwoThousandCharacters_IsRejected()
    {
        var request = new CreateProjectViewModel
        {
            ClientId = Guid.NewGuid(),
            Name = "Website Redesign",
            Description = new string('a', 2001),
            OwnerUserId = "user-42",
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateProjectViewModel.Description)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankOwnerUserId_IsRejected(string blankOwnerUserId)
    {
        var request = new CreateProjectViewModel
        {
            ClientId = Guid.NewGuid(),
            Name = "Website Redesign",
            OwnerUserId = blankOwnerUserId,
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateProjectViewModel.OwnerUserId)));
    }

    [Fact]
    public void Validate_NotesLongerThanTwoThousandCharacters_IsRejected()
    {
        var request = new CreateProjectViewModel
        {
            ClientId = Guid.NewGuid(),
            Name = "Website Redesign",
            Notes = new string('a', 2001),
            OwnerUserId = "user-42",
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateProjectViewModel.Notes)));
    }

    private static IReadOnlyList<ValidationResult> Validate(CreateProjectViewModel request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
