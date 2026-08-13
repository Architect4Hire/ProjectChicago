using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Models.DataModels.Entities;

// Entity-level invariant tests only (CLIENT-001..015, DATA-006..008). No EF/persistence
// involvement - these assert what Client.Create enforces regardless of how it is later stored.
public class ClientTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Client CreateValidClient(
        ClientLifecycleStatus lifecycleStatus = ClientLifecycleStatus.Lead,
        DateTime? createdAtUtc = null) =>
        Client.Create(
            id: Guid.NewGuid(),
            name: "Acme Corporation",
            lifecycleStatus: lifecycleStatus,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: createdAtUtc ?? CreatedAtUtc,
            primaryContactName: "Jane Doe",
            primaryEmail: "jane@acme.example",
            primaryPhone: "+1-555-0100",
            website: "https://acme.example",
            addressLine: "123 Main St",
            city: "Springfield",
            stateOrProvince: "IL",
            postalCode: "62704",
            country: "US",
            description: "Longtime client.");

    [Fact]
    public void Create_WithValidArguments_SetsAllProvidedValues()
    {
        var id = Guid.NewGuid();

        var client = Client.Create(
            id: id,
            name: "Acme Corporation",
            lifecycleStatus: ClientLifecycleStatus.Prospect,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc,
            primaryContactName: "Jane Doe",
            primaryEmail: "jane@acme.example",
            primaryPhone: "+1-555-0100",
            website: "https://acme.example",
            addressLine: "123 Main St",
            city: "Springfield",
            stateOrProvince: "IL",
            postalCode: "62704",
            country: "US",
            description: "Longtime client.");

        Assert.Equal(id, client.Id);
        Assert.Equal("Acme Corporation", client.Name);
        Assert.Equal(ClientLifecycleStatus.Prospect, client.LifecycleStatus);
        Assert.Equal("owner-1", client.OwnerUserId);
        Assert.Equal("Jane Doe", client.PrimaryContactName);
        Assert.Equal("jane@acme.example", client.PrimaryEmail);
        Assert.Equal("+1-555-0100", client.PrimaryPhone);
        Assert.Equal("https://acme.example", client.Website);
        Assert.Equal("123 Main St", client.AddressLine);
        Assert.Equal("Springfield", client.City);
        Assert.Equal("IL", client.StateOrProvince);
        Assert.Equal("62704", client.PostalCode);
        Assert.Equal("US", client.Country);
        Assert.Equal("Longtime client.", client.Description);
    }

    [Fact]
    public void Create_WithoutOptionalContactAndAddressFields_LeavesThemNull()
    {
        var client = Client.Create(
            id: Guid.NewGuid(),
            name: "Acme Corporation",
            lifecycleStatus: ClientLifecycleStatus.Lead,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc);

        Assert.Null(client.PrimaryContactName);
        Assert.Null(client.PrimaryEmail);
        Assert.Null(client.PrimaryPhone);
        Assert.Null(client.Website);
        Assert.Null(client.AddressLine);
        Assert.Null(client.City);
        Assert.Null(client.StateOrProvince);
        Assert.Null(client.PostalCode);
        Assert.Null(client.Country);
        Assert.Null(client.Description);
    }

    [Fact]
    public void Create_SetsLastModifiedMetadataEqualToCreatedMetadata()
    {
        var client = CreateValidClient();

        Assert.Equal(client.CreatedAtUtc, client.LastModifiedAtUtc);
        Assert.Equal(client.CreatedBy, client.LastModifiedBy);
    }

    [Fact]
    public void Create_AssignsAnEmptyRowVersion_UntilPersistence()
    {
        var client = CreateValidClient();

        Assert.Empty(client.RowVersion);
    }

    [Fact]
    public void Create_WithEmptyId_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => Client.Create(
            id: Guid.Empty,
            name: "Acme Corporation",
            lifecycleStatus: ClientLifecycleStatus.Lead,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("id", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceName_Throws(string? name)
    {
        var exception = Assert.Throws<ArgumentException>(() => Client.Create(
            id: Guid.NewGuid(),
            name: name!,
            lifecycleStatus: ClientLifecycleStatus.Lead,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("name", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceOwnerUserId_Throws(string? ownerUserId)
    {
        var exception = Assert.Throws<ArgumentException>(() => Client.Create(
            id: Guid.NewGuid(),
            name: "Acme Corporation",
            lifecycleStatus: ClientLifecycleStatus.Lead,
            ownerUserId: ownerUserId!,
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("ownerUserId", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceCreatedBy_Throws(string? createdBy)
    {
        var exception = Assert.Throws<ArgumentException>(() => Client.Create(
            id: Guid.NewGuid(),
            name: "Acme Corporation",
            lifecycleStatus: ClientLifecycleStatus.Lead,
            ownerUserId: "owner-1",
            createdBy: createdBy!,
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("createdBy", exception.ParamName);
    }

    [Fact]
    public void Create_WithUndefinedLifecycleStatus_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => Client.Create(
            id: Guid.NewGuid(),
            name: "Acme Corporation",
            lifecycleStatus: (ClientLifecycleStatus)999,
            ownerUserId: "owner-1",
            createdBy: "creator-1",
            createdAtUtc: CreatedAtUtc));

        Assert.Equal("lifecycleStatus", exception.ParamName);
    }

    [Theory]
    [InlineData(ClientLifecycleStatus.Lead)]
    [InlineData(ClientLifecycleStatus.Prospect)]
    [InlineData(ClientLifecycleStatus.Active)]
    [InlineData(ClientLifecycleStatus.OnHold)]
    [InlineData(ClientLifecycleStatus.Inactive)]
    [InlineData(ClientLifecycleStatus.Archived)]
    public void Create_AllowsEveryInitialLifecycleStatus_FromClient010(ClientLifecycleStatus status)
    {
        var client = CreateValidClient(lifecycleStatus: status);

        Assert.Equal(status, client.LifecycleStatus);
    }

    [Fact]
    public void Create_WithLocalCreatedAtUtc_Throws()
    {
        var localTime = DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Local);

        var exception = Assert.Throws<ArgumentException>(() => CreateValidClient(createdAtUtc: localTime));

        Assert.Equal("createdAtUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WithUnspecifiedCreatedAtUtcKind_Throws()
    {
        var unspecifiedTime = DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Unspecified);

        var exception = Assert.Throws<ArgumentException>(() => CreateValidClient(createdAtUtc: unspecifiedTime));

        Assert.Equal("createdAtUtc", exception.ParamName);
    }

    // --- ChangeLifecycleStatus (CLIENT-010..015) ---
    //
    // These are the low-level invariants ChangeLifecycleStatus itself enforces. Whether a
    // particular status-to-status transition is allowed is a Business-layer policy decision
    // (ClientLifecycleTransitionRules) exercised in ClientBusinessTests, not here - this entity
    // method accepts any defined status change and never inspects the previous status.

    [Fact]
    public void ChangeLifecycleStatus_UpdatesStatusAndLastModifiedMetadata()
    {
        var client = CreateValidClient(lifecycleStatus: ClientLifecycleStatus.Lead);
        var modifiedAtUtc = CreatedAtUtc.AddDays(1);

        client.ChangeLifecycleStatus(ClientLifecycleStatus.Active, "modifier-1", modifiedAtUtc);

        Assert.Equal(ClientLifecycleStatus.Active, client.LifecycleStatus);
        Assert.Equal("modifier-1", client.LastModifiedBy);
        Assert.Equal(modifiedAtUtc, client.LastModifiedAtUtc);
    }

    [Fact]
    public void ChangeLifecycleStatus_DoesNotChangeCreatedMetadata()
    {
        var client = CreateValidClient(lifecycleStatus: ClientLifecycleStatus.Lead);

        client.ChangeLifecycleStatus(ClientLifecycleStatus.Active, "modifier-1", CreatedAtUtc.AddDays(1));

        Assert.Equal(CreatedAtUtc, client.CreatedAtUtc);
        Assert.Equal("creator-1", client.CreatedBy);
    }

    [Fact]
    public void ChangeLifecycleStatus_WithAnUndefinedStatus_Throws()
    {
        var client = CreateValidClient();

        var exception = Assert.Throws<ArgumentException>(
            () => client.ChangeLifecycleStatus((ClientLifecycleStatus)999, "modifier-1", CreatedAtUtc.AddDays(1)));

        Assert.Equal("newStatus", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangeLifecycleStatus_WithNullOrWhitespaceModifiedBy_Throws(string? modifiedBy)
    {
        var client = CreateValidClient();

        var exception = Assert.Throws<ArgumentException>(
            () => client.ChangeLifecycleStatus(ClientLifecycleStatus.Active, modifiedBy!, CreatedAtUtc.AddDays(1)));

        Assert.Equal("modifiedBy", exception.ParamName);
    }

    [Fact]
    public void ChangeLifecycleStatus_WithLocalModifiedAtUtc_Throws()
    {
        var client = CreateValidClient();
        var localTime = DateTime.SpecifyKind(CreatedAtUtc.AddDays(1), DateTimeKind.Local);

        var exception = Assert.Throws<ArgumentException>(
            () => client.ChangeLifecycleStatus(ClientLifecycleStatus.Active, "modifier-1", localTime));

        Assert.Equal("modifiedAtUtc", exception.ParamName);
    }
}
