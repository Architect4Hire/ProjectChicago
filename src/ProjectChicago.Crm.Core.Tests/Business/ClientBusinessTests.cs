using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Models.ServiceModels;
using ProjectChicago.Shared.Correlation;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Business;

// Pure unit tests for ClientBusiness (CLIENT-001..004, AUDIT-001..003; backend.md Tests: "Unit-test
// Facade/Business/Data behavior at the layer that owns the rule"). IClientData is faked rather than
// backed by SQL Server - proving Business's own rules/translation does not require a database,
// matching the RESTRICTION that Business itself never touches EF.
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

    private static CreateClientCommand CreateCommand(
        string name = "Acme Corporation",
        string? primaryEmail = "Jane@Acme.example",
        string? primaryPhone = "+1-555-0100",
        ClientLifecycleStatus? lifecycleStatus = null,
        ActorContext? actor = null) => new()
    {
        Name = name,
        OwnerUserId = "owner-1",
        PrimaryEmail = primaryEmail,
        PrimaryPhone = primaryPhone,
        LifecycleStatus = lifecycleStatus,
        Actor = actor ?? ActorContext.ForUser("user-1"),
        RequestContext = RequestContext.CreateNew(),
        CreatedAtUtc = CreatedAtUtc,
    };

    // --- Initial state (CLIENT-010) ---

    [Fact]
    public async Task CreateAsync_WithNoLifecycleStatusSupplied_DefaultsToLead()
    {
        var business = new ClientBusiness(new FakeClientData());

        var result = await business.CreateAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(ClientLifecycleStatus.Lead, result.Client.LifecycleStatus);
    }

    [Fact]
    public async Task CreateAsync_WithAnExplicitLifecycleStatus_UsesIt()
    {
        var business = new ClientBusiness(new FakeClientData());

        var result = await business.CreateAsync(
            CreateCommand(lifecycleStatus: ClientLifecycleStatus.Active), CancellationToken.None);

        Assert.Equal(ClientLifecycleStatus.Active, result.Client.LifecycleStatus);
    }

    [Fact]
    public async Task CreateAsync_WithAnUndefinedLifecycleStatus_Throws()
    {
        var business = new ClientBusiness(new FakeClientData());
        var command = CreateCommand() with { LifecycleStatus = (ClientLifecycleStatus)999 };

        await Assert.ThrowsAsync<ArgumentException>(() => business.CreateAsync(command, CancellationToken.None));
    }

    // --- Model translation ---

    [Fact]
    public async Task CreateAsync_TrimsNameAndLowercasesEmail()
    {
        var business = new ClientBusiness(new FakeClientData());

        var result = await business.CreateAsync(
            CreateCommand(name: "  Acme Corporation  ", primaryEmail: "Jane@Acme.EXAMPLE"), CancellationToken.None);

        Assert.Equal("Acme Corporation", result.Client.Name);
        Assert.Equal("jane@acme.example", result.Client.PrimaryEmail);
    }

    [Fact]
    public async Task CreateAsync_ConvertsBlankOptionalFieldsToNull()
    {
        var business = new ClientBusiness(new FakeClientData());
        var command = CreateCommand() with { Website = "   ", Description = "" };

        var result = await business.CreateAsync(command, CancellationToken.None);

        Assert.Null(result.Client.Website);
        Assert.Null(result.Client.Description);
    }

    [Fact]
    public async Task CreateAsync_AssignsAFreshApplicationGeneratedId()
    {
        var business = new ClientBusiness(new FakeClientData());

        var result = await business.CreateAsync(CreateCommand(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Client.Id);
    }

    [Fact]
    public async Task CreateAsync_UsesTheActorIdAsCreatedByAndLastModifiedBy()
    {
        var business = new ClientBusiness(new FakeClientData());

        var result = await business.CreateAsync(
            CreateCommand(actor: ActorContext.ForUser("actor-42")), CancellationToken.None);

        Assert.Equal("actor-42", result.Client.CreatedBy);
        Assert.Equal("actor-42", result.Client.LastModifiedBy);
    }

    [Fact]
    public async Task CreateAsync_WithASystemActor_ThrowsBecauseCreatedByCannotBeAttributed()
    {
        var business = new ClientBusiness(new FakeClientData());

        await Assert.ThrowsAsync<ArgumentException>(
            () => business.CreateAsync(CreateCommand(actor: ActorContext.ForSystem()), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_CallsClientDataCreateAsyncExactlyOnce_WithTheBuiltClient()
    {
        var data = new FakeClientData();
        var business = new ClientBusiness(data);

        var result = await business.CreateAsync(CreateCommand(), CancellationToken.None);

        Assert.Same(result.Client, data.CreatedClient);
    }

    [Fact]
    public async Task CreateAsync_LooksUpDuplicatesUsingNormalizedValues_BeforeCreating()
    {
        var data = new FakeClientData();
        var business = new ClientBusiness(data);

        await business.CreateAsync(
            CreateCommand(name: "  Acme Corporation  ", primaryEmail: "Jane@Acme.EXAMPLE"), CancellationToken.None);

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

        var result = await business.CreateAsync(
            CreateCommand(name: "Acme Corporation", primaryEmail: "jane@acme.example"), CancellationToken.None);

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

        var result = await business.CreateAsync(CreateCommand(name: "Acme Corporation"), CancellationToken.None);

        Assert.NotNull(data.CreatedClient);
        Assert.NotEqual(existing.Id, result.Client.Id);
    }

    // --- Emitted audit fact (AUDIT-001..003) ---

    [Fact]
    public async Task CreateAsync_EmitsACreatedAuditFact_WithSourceServiceEntityTypeAndAction()
    {
        var data = new FakeClientData();
        var business = new ClientBusiness(data);

        var result = await business.CreateAsync(CreateCommand(), CancellationToken.None);

        var fact = data.CreatedAuditFact!;
        Assert.Equal(AuditSourceServices.Crm, fact.SourceService);
        Assert.Equal(AuditEntityTypes.Client, fact.EntityType);
        Assert.Equal(AuditActions.Created, fact.Action);
        Assert.Equal(result.Client.Id, fact.EntityId);
        Assert.Equal(AuditActorTypes.User, fact.ActorType);
        Assert.Equal("user-1", fact.ActorId);
    }

    [Fact]
    public async Task CreateAsync_AuditFactPreservesCorrelationTraceAndCausationIds()
    {
        var data = new FakeClientData();
        var business = new ClientBusiness(data);
        var requestContext = RequestContext.CreateNew(ActorContext.ForUser("user-1")).CreateCaused();
        var command = CreateCommand() with { RequestContext = requestContext };

        await business.CreateAsync(command, CancellationToken.None);

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

        await business.CreateAsync(CreateCommand(), CancellationToken.None);

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

        await business.CreateAsync(CreateCommand(), CancellationToken.None);

        Assert.Null(data.CreatedAuditFact!.PreviousValues);
        Assert.Null(data.CreatedAuditFact!.NewValues);
    }
}
