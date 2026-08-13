using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ProjectChicago.Crm.Contracts.Clients;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests.Contracts.Clients;

// Locks the POST /api/clients request wire shape (CLIENT-001..004, API-001..007) and its
// transport-level shape/format validation (SEC-022) independently of any future controller/MVC
// JSON configuration - every property carries an explicit [JsonPropertyName], so these
// expectations hold under a plain System.Text.Json.JsonSerializer.Serialize/Deserialize call.
public class CreateClientViewModelContractTests
{
    [Fact]
    public void Serialize_FullRequest_UsesCamelCasePropertyNamesAndStringLifecycleStatus()
    {
        var request = new CreateClientViewModel
        {
            Name = "Acme Corporation",
            PrimaryContactName = "Jamie Rivera",
            PrimaryEmail = "jamie@acme.example",
            PrimaryPhone = "+1-555-0100",
            Website = "https://acme.example",
            AddressLine = "1 Acme Way",
            City = "Springfield",
            StateOrProvince = "IL",
            PostalCode = "62704",
            Country = "USA",
            LifecycleStatus = ClientLifecycleStatusContract.Prospect,
            Description = "Longstanding prospective client.",
            OwnerUserId = "user-42",
        };

        var json = JsonSerializer.Serialize(request);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal("Acme Corporation", root.GetProperty("name").GetString());
        Assert.Equal("Jamie Rivera", root.GetProperty("primaryContactName").GetString());
        Assert.Equal("jamie@acme.example", root.GetProperty("primaryEmail").GetString());
        Assert.Equal("+1-555-0100", root.GetProperty("primaryPhone").GetString());
        Assert.Equal("https://acme.example", root.GetProperty("website").GetString());
        Assert.Equal("1 Acme Way", root.GetProperty("addressLine").GetString());
        Assert.Equal("Springfield", root.GetProperty("city").GetString());
        Assert.Equal("IL", root.GetProperty("stateOrProvince").GetString());
        Assert.Equal("62704", root.GetProperty("postalCode").GetString());
        Assert.Equal("USA", root.GetProperty("country").GetString());
        Assert.Equal("user-42", root.GetProperty("ownerUserId").GetString());

        // Stable string enum serialization, not the numeric backing value (api-contracts.md).
        Assert.Equal("Prospect", root.GetProperty("lifecycleStatus").GetString());
    }

    [Fact]
    public void Deserialize_MinimalPayload_LeavesOptionalFieldsNull()
    {
        const string json = """
            {
                "name": "Acme Corporation",
                "ownerUserId": "user-42"
            }
            """;

        var request = JsonSerializer.Deserialize<CreateClientViewModel>(json);

        Assert.NotNull(request);
        Assert.Equal("Acme Corporation", request!.Name);
        Assert.Equal("user-42", request.OwnerUserId);
        Assert.Null(request.PrimaryContactName);
        Assert.Null(request.PrimaryEmail);
        Assert.Null(request.LifecycleStatus);
        Assert.Null(request.Description);
    }

    [Fact]
    public void Deserialize_MissingRequiredName_ThrowsBeforeAnyValidatorRuns()
    {
        // Missing a `required` C# member is rejected by System.Text.Json itself (net7+), giving a
        // transport-level 400 for the two mandatory contract fields even before Facade validators
        // run (add-endpoint.md step 2: "Transport model validation catches shape/format").
        const string json = """{ "ownerUserId": "user-42" }""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateClientViewModel>(json));
    }

    [Fact]
    public void Deserialize_MissingRequiredOwnerUserId_ThrowsBeforeAnyValidatorRuns()
    {
        const string json = """{ "name": "Acme Corporation" }""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateClientViewModel>(json));
    }

    [Fact]
    public void Validate_FullyPopulatedValidRequest_ProducesNoValidationErrors()
    {
        var request = new CreateClientViewModel
        {
            Name = "Acme Corporation",
            PrimaryEmail = "jamie@acme.example",
            Website = "https://acme.example",
            OwnerUserId = "user-42",
        };

        var errors = Validate(request);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankName_IsRejected(string blankName)
    {
        var request = new CreateClientViewModel { Name = blankName, OwnerUserId = "user-42" };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateClientViewModel.Name)));
    }

    [Fact]
    public void Validate_NameLongerThanTwoHundredCharacters_IsRejected()
    {
        var request = new CreateClientViewModel { Name = new string('a', 201), OwnerUserId = "user-42" };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateClientViewModel.Name)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankOwnerUserId_IsRejected(string blankOwnerUserId)
    {
        var request = new CreateClientViewModel { Name = "Acme Corporation", OwnerUserId = blankOwnerUserId };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateClientViewModel.OwnerUserId)));
    }

    [Fact]
    public void Validate_MalformedPrimaryEmail_IsRejected()
    {
        var request = new CreateClientViewModel
        {
            Name = "Acme Corporation",
            OwnerUserId = "user-42",
            PrimaryEmail = "not-an-email",
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateClientViewModel.PrimaryEmail)));
    }

    [Fact]
    public void Validate_MalformedWebsite_IsRejected()
    {
        var request = new CreateClientViewModel
        {
            Name = "Acme Corporation",
            OwnerUserId = "user-42",
            Website = "not a url",
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateClientViewModel.Website)));
    }

    private static IReadOnlyList<ValidationResult> Validate(CreateClientViewModel request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
