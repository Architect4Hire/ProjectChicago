using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Shared.Correlation;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Business;

// Pure unit tests for ClientBusiness (CLIENT-001..004, AUDIT-001..003; backend.md Tests: "Unit-test
// Facade/Business/Data behavior at the layer that owns the rule"). IClientData is faked rather than
// backed by SQL Server - proving Business's own rules/translation does not require a database,
// matching the RESTRICTION that Business itself never touches EF. CreateAsync takes the wire
// CreateClientViewModel and returns the wire ClientServiceModel directly (Business owns that
// mapping - ClientContractMappingExtensions), so these tests assert against ClientServiceModel's
// fields rather than an internal Client-entity wrapper.
public class ClientBusinessTests
{
    private sealed class FakeClientData : IClientData
    {
        public Client? CreatedClient { get; private set; }

        public EntityMutationAudited? CreatedAuditFact { get; private set; }

        public IReadOnlyList<Client> DuplicateCandidatesToReturn { get; init; } = [];

        public string? DuplicateLookupName { get; private set; }

        public string? DuplicateLookupEmail { get; private set; }

        public string? DuplicateLookupPhone { get; private set; }

        public Task CreateAsync(Client client, EntityMutationAudited auditFact, CancellationToken cancellationToken)
        {
            CreatedClient = client;
            CreatedAuditFact = auditFact;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Client>> FindDuplicateCandidatesAsync(
            string? normalizedName,
            string? normalizedEmail,
            string? normalizedPhone,
            CancellationToken cancellationToken)
        {
            DuplicateLookupName = normalizedName;
            DuplicateLookupEmail = normalizedEmail;
            DuplicateLookupPhone = normalizedPhone;
            return Task.FromResult(DuplicateCandidatesToReturn);
        }
    }

    private static readonly DateTime CreatedAtUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static CreateClientViewModel CreateViewModel(
        string name = "Acme Corporation",
        string? primaryEmail = "Jane@Acme.example",
        string? primaryPhone = "+1-555-0100",
        ClientLifecycleStatusContract? lifecycleStatus = null) => new()
    {
        Name = name,
        OwnerUserId = "owner-1",
        PrimaryEmail = primaryEmail,
        PrimaryPhone = primaryPhone,
        LifecycleStatus = lifecycleStatus,
    };

    private static Task<ClientServiceModel> CreateAsync(
        ClientBusiness business,
        CreateClientViewModel request,
        ActorContext? actor = null,
        RequestContext? requestContext = null) =>
        business.CreateAsync(
            request,
            actor ?? ActorContext.ForUser("user-1"),
            requestContext ?? RequestContext.CreateNew(),
            CreatedAtUtc,
            CancellationToken.None);

    // --- Initial state (CLIENT-010) ---

    [Fact]
    public async Task CreateAsync_WithNoLifecycleStatusSupplied_DefaultsToLead()
    {
        var business = new ClientBusiness(new FakeClientData());

        var result = await CreateAsync(business, CreateViewModel());

        Assert.Equal(ClientLifecycleStatusContract.Lead, result.LifecycleStatus);
    }

    [Fact]
    public async Task CreateAsync_WithAnExplicitLifecycleStatus_UsesIt()
    {
        var business = new ClientBusiness(new FakeClientData());

        var result = await CreateAsync(business, CreateViewModel(lifecycleStatus: ClientLifecycleStatusContract.Active));

        Assert.Equal(ClientLifecycleStatusContract.Active, result.LifecycleStatus);
    }

    [Fact]
    public async Task CreateAsync_WithAnUndefinedLifecycleStatus_Throws()
    {
        var business = new ClientBusiness(new FakeClientData());
        var request = CreateViewModel() with { LifecycleStatus = (ClientLifecycleStatusContract)999 };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateAsync(business, request));
    }

    // --- Model translation ---

    [Fact]
    public async Task CreateAsync_TrimsNameAndLowercasesEmail()
    {
        var business = new ClientBusiness(new FakeClientData());

        var result = await CreateAsync(
            business, CreateViewModel(name: "  Acme Corporation  ", primaryEmail: "Jane@Acme.EXAMPLE"));

        Assert.Equal("Acme Corporation", result.Name);
        Assert.Equal("jane@acme.example", result.PrimaryEmail);
    }

    [Fact]
    public async Task CreateAsync_ConvertsBlankOptionalFieldsToNull()
    {
        var business = new ClientBusiness(new FakeClientData());
        var request = CreateViewModel() with { Website = "   ", Description = "" };

        var result = await CreateAsync(business, request);

        Assert.Null(result.Website);
        Assert.Null(result.Description);
    }

    [Fact]
    public async Task CreateAsync_AssignsAFreshApplicationGeneratedId()
    {
        var business = new ClientBusiness(new FakeClientData());

        var result = await CreateAsync(business, CreateViewModel());

        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task CreateAsync_UsesTheActorIdAsCreatedByAndLastModifiedBy()
    {
        var business = new ClientBusiness(new FakeClientData());

        var result = await CreateAsync(business, CreateViewModel(), actor: ActorContext.ForUser("actor-42"));

        Assert.Equal("actor-42", result.CreatedBy);
        Assert.Equal("actor-42", result.LastModifiedBy);
    }

    [Fact]
    public async Task CreateAsync_WithASystemActor_ThrowsBecauseCreatedByCannotBeAttributed()
    {
        var business = new ClientBusiness(new FakeClientData());

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAsync(business, CreateViewModel(), actor: ActorContext.ForSystem()));
    }

    [Fact]
    public async Task CreateAsync_CallsClientDataCreateAsyncExactlyOnce_WithTheBuiltClient()
    {
        var data = new FakeClientData();
        var business = new ClientBusiness(data);

        var result = await CreateAsync(business, CreateViewModel());

        Assert.NotNull(data.CreatedClient);
        Assert.Equal(result.Id, data.CreatedClient!.Id);
    }

    [Fact]
    public async Task CreateAsync_LooksUpDuplicatesUsingNormalizedValues_BeforeCreating()
    {
        var data = new FakeClientData();
        var business = new ClientBusiness(data);

        await CreateAsync(business, CreateViewModel(name: "  Acme Corporation  ", primaryEmail: "Jane@Acme.EXAMPLE"));

        Assert.Equal("Acme Corporation", data.DuplicateLookupName);
        Assert.Equal("jane@acme.example", data.DuplicateLookupEmail);
        Assert.Equal("+1-555-0100", data.DuplicateLookupPhone);
    }

    [Fact]
    public async Task CreateAsync_ReturnsPossibleDuplicatesWithTheFieldsThatMatched()
    {
        var existingId = Guid.NewGuid();
        var existing = Client.Create(
            id: existingId,
            name: "Acme Corporation",
            lifecycleStatus: ClientLifecycleStatus.Active,
            ownerUserId: "owner-2",
            createdBy: "creator-2",
            createdAtUtc: CreatedAtUtc,
            primaryEmail: "someone-else@example.com");
        var data = new FakeClientData { DuplicateCandidatesToReturn = [existing] };
        var business = new ClientBusiness(data);

        var result = await CreateAsync(
            business, CreateViewModel(name: "Acme Corporation", primaryEmail: "jane@acme.example"));

        var duplicate = Assert.Single(result.PossibleDuplicates);
        Assert.Equal(existingId, duplicate.ClientId);
        Assert.Contains(ClientDuplicateMatchField.Name, duplicate.MatchedOn);
        Assert.DoesNotContain(ClientDuplicateMatchField.PrimaryEmail, duplicate.MatchedOn);
    }

    [Fact]
    public async Task CreateAsync_StillCreatesTheClient_WhenDuplicatesAreFound()
    {
        var existing = Client.Create(
            id: Guid.NewGuid(),
            name: "Acme Corporation",
            lifecycleStatus: ClientLifecycleStatus.Active,
            ownerUserId: "owner-2",
            createdBy: "creator-2",
            createdAtUtc: CreatedAtUtc);
        var data = new FakeClientData { DuplicateCandidatesToReturn = [existing] };
        var business = new ClientBusiness(data);

        var result = await CreateAsync(business, CreateViewModel(name: "Acme Corporation"));

        Assert.NotNull(data.CreatedClient);
        Assert.NotEqual(existing.Id, result.Id);
    }

    // --- Emitted audit fact (AUDIT-001..003) ---

    [Fact]
    public async Task CreateAsync_EmitsACreatedAuditFact_WithSourceServiceEntityTypeAndAction()
    {
        var data = new FakeClientData();
        var business = new ClientBusiness(data);

        var result = await CreateAsync(business, CreateViewModel());

        var fact = data.CreatedAuditFact!;
        Assert.Equal(AuditSourceServices.Crm, fact.SourceService);
        Assert.Equal(AuditEntityTypes.Client, fact.EntityType);
        Assert.Equal(AuditActions.Created, fact.Action);
        Assert.Equal(result.Id, fact.EntityId);
        Assert.Equal(AuditActorTypes.User, fact.ActorType);
        Assert.Equal("user-1", fact.ActorId);
    }

    [Fact]
    public async Task CreateAsync_AuditFactPreservesCorrelationTraceAndCausationIds()
    {
        var data = new FakeClientData();
        var business = new ClientBusiness(data);
        var requestContext = RequestContext.CreateNew(ActorContext.ForUser("user-1")).CreateCaused();

        await CreateAsync(business, CreateViewModel(), requestContext: requestContext);

        var fact = data.CreatedAuditFact!;
        Assert.Equal(requestContext.TraceId, fact.TraceId);
        Assert.Equal(requestContext.CorrelationId, fact.CorrelationId);
        Assert.Equal(requestContext.CausationId, fact.CausationId);
    }

    [Fact]
    public async Task CreateAsync_AuditFactChangedFieldsListsOnlyPopulatedBusinessFields()
    {
        var data = new FakeClientData();
        var business = new ClientBusiness(data);

        await CreateAsync(business, CreateViewModel());

        var changedFields = data.CreatedAuditFact!.ChangedFields;
        Assert.Contains(nameof(Client.Name), changedFields);
        Assert.Contains(nameof(Client.PrimaryEmail), changedFields);
        Assert.Contains(nameof(Client.PrimaryPhone), changedFields);
        Assert.DoesNotContain(nameof(Client.PrimaryContactName), changedFields);
        Assert.DoesNotContain(nameof(Client.Website), changedFields);
    }

    [Fact]
    public async Task CreateAsync_AuditFactNeverCarriesPreviousOrNewValues()
    {
        var data = new FakeClientData();
        var business = new ClientBusiness(data);

        await CreateAsync(business, CreateViewModel());

        Assert.Null(data.CreatedAuditFact!.PreviousValues);
        Assert.Null(data.CreatedAuditFact!.NewValues);
    }
}
