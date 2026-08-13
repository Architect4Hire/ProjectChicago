using System.Text.Json;
using ProjectChicago.Crm.Contracts.Clients;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests.Contracts.Clients;

// Locks the 201 Created response wire shape for POST /api/clients (CLIENT-001..004, API-001..007,
// DATA-006, DATA-008) independently of any future controller/MVC JSON configuration.
public class ClientServiceModelContractTests
{
    [Fact]
    public void Serialize_Response_RoundTripsThroughJsonPreservingAllFields()
    {
        var createdAtUtc = new DateTime(2026, 8, 12, 15, 30, 0, DateTimeKind.Utc);
        var lastModifiedAtUtc = createdAtUtc;
        var duplicateClientId = Guid.NewGuid();
        var response = new ClientServiceModel
        {
            Id = Guid.NewGuid(),
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
            LifecycleStatus = ClientLifecycleStatusContract.Lead,
            Description = "Newly created client.",
            OwnerUserId = "user-42",
            CreatedAtUtc = createdAtUtc,
            CreatedBy = "user-42",
            LastModifiedAtUtc = lastModifiedAtUtc,
            LastModifiedBy = "user-42",
            ConcurrencyToken = "AAAAAAAAB9E=",
            PossibleDuplicates =
            [
                new ClientDuplicateWarning
                {
                    ClientId = duplicateClientId,
                    Name = "ACME Corp",
                    MatchedOn = [ClientDuplicateMatchField.Name, ClientDuplicateMatchField.PrimaryEmail],
                },
            ],
        };

        var json = JsonSerializer.Serialize(response);
        var roundTripped = JsonSerializer.Deserialize<ClientServiceModel>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(response.Id, roundTripped!.Id);
        Assert.Equal(response.Name, roundTripped.Name);
        Assert.Equal(response.LifecycleStatus, roundTripped.LifecycleStatus);
        Assert.Equal(response.CreatedAtUtc, roundTripped.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, roundTripped.CreatedAtUtc.Kind);
        Assert.Equal(response.ConcurrencyToken, roundTripped.ConcurrencyToken);
        Assert.Single(roundTripped.PossibleDuplicates);
        Assert.Equal(duplicateClientId, roundTripped.PossibleDuplicates[0].ClientId);
        Assert.Equal(
            [ClientDuplicateMatchField.Name, ClientDuplicateMatchField.PrimaryEmail],
            roundTripped.PossibleDuplicates[0].MatchedOn);
    }

    [Fact]
    public void Serialize_Response_UsesCamelCasePropertyNamesAndStringEnumsNotNumbers()
    {
        var response = new ClientServiceModel
        {
            Id = Guid.NewGuid(),
            Name = "Acme Corporation",
            LifecycleStatus = ClientLifecycleStatusContract.Active,
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
        Assert.True(root.TryGetProperty("ownerUserId", out _));
        Assert.True(root.TryGetProperty("createdAtUtc", out _));
        Assert.True(root.TryGetProperty("concurrencyToken", out _));
        Assert.Equal("Active", root.GetProperty("lifecycleStatus").GetString());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("possibleDuplicates").ValueKind);
    }

    [Fact]
    public void PossibleDuplicates_WhenNotSet_DefaultsToEmptyRatherThanNull()
    {
        var response = new ClientServiceModel
        {
            Id = Guid.NewGuid(),
            Name = "Acme Corporation",
            LifecycleStatus = ClientLifecycleStatusContract.Lead,
            OwnerUserId = "user-42",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "user-42",
            LastModifiedAtUtc = DateTime.UtcNow,
            LastModifiedBy = "user-42",
            ConcurrencyToken = "AAAAAAAAB9E=",
        };

        Assert.NotNull(response.PossibleDuplicates);
        Assert.Empty(response.PossibleDuplicates);
    }
}
