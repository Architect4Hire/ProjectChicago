using System.ComponentModel.DataAnnotations;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Crm.Core.Facades;
using ProjectChicago.Shared.Correlation;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Facades;

// Pure unit tests for ClientFacade (CLIENT-001..004, SEC-010..013; backend.md Tests: "Unit-test
// Facade/Business/Data behavior at the layer that owns the rule"). IClientBusiness,
// IClientAuthorization, ICurrentRequestContext, and IClock are faked rather than backed by real
// infrastructure - proving the Facade's own authorization-call/validation/orchestration rules do
// not require EF, HTTP, or a real clock (RESTRICTION: this scope never touches Data/EF).
// CreateAsync's contract is "authorize, validate, resolve context, delegate, return unchanged" -
// the ViewModel<->ServiceModel mapping itself belongs to IClientBusiness/ClientBusinessTests, not
// here.
public class ClientFacadeTests
{
    private sealed class FakeClientBusiness : IClientBusiness
    {
        public CreateClientViewModel? ReceivedRequest { get; private set; }

        public ActorContext? ReceivedActor { get; private set; }

        public RequestContext? ReceivedRequestContext { get; private set; }

        public DateTime? ReceivedCreatedAtUtc { get; private set; }

        public bool WasCalled { get; private set; }

        public ClientServiceModel ResultToReturn { get; init; } = BuildDefaultResult();

        public Task<ClientServiceModel> CreateAsync(
            CreateClientViewModel request,
            ActorContext actor,
            RequestContext requestContext,
            DateTime createdAtUtc,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedRequest = request;
            ReceivedActor = actor;
            ReceivedRequestContext = requestContext;
            ReceivedCreatedAtUtc = createdAtUtc;
            return Task.FromResult(ResultToReturn);
        }

        private static ClientServiceModel BuildDefaultResult() => new()
        {
            Id = Guid.NewGuid(),
            Name = "Acme Corporation",
            LifecycleStatus = ClientLifecycleStatusContract.Lead,
            OwnerUserId = "owner-1",
            CreatedAtUtc = FixedUtcNow,
            CreatedBy = "user-1",
            LastModifiedAtUtc = FixedUtcNow,
            LastModifiedBy = "user-1",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
        };
    }

    private sealed class FakeClientAuthorization : IClientAuthorization
    {
        public bool AuthorizedResult { get; init; } = true;

        public ActorContext? ReceivedActor { get; private set; }

        public bool WasCalled { get; private set; }

        public Task<bool> CanCreateAsync(ActorContext actor, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedActor = actor;
            return Task.FromResult(AuthorizedResult);
        }
    }

    private sealed class FakeCurrentRequestContext : ICurrentRequestContext
    {
        public required RequestContext Current { get; init; }
    }

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; init; } = FixedUtcNow;
    }

    private static readonly DateTime FixedUtcNow = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static CreateClientViewModel CreateRequest(
        string name = "Acme Corporation",
        string ownerUserId = "owner-1",
        string? primaryEmail = "Jane@Acme.example",
        ClientLifecycleStatusContract? lifecycleStatus = null) => new()
    {
        Name = name,
        OwnerUserId = ownerUserId,
        PrimaryEmail = primaryEmail,
        LifecycleStatus = lifecycleStatus,
    };

    private static ClientFacade BuildFacade(
        out FakeClientBusiness business,
        out FakeClientAuthorization authorization,
        bool authorized = true,
        ActorContext? actor = null,
        ClientServiceModel? businessResult = null)
    {
        business = businessResult is null
            ? new FakeClientBusiness()
            : new FakeClientBusiness { ResultToReturn = businessResult };
        authorization = new FakeClientAuthorization { AuthorizedResult = authorized };

        var requestContext = new FakeCurrentRequestContext
        {
            Current = RequestContext.CreateNew(actor ?? ActorContext.ForUser("user-1")),
        };

        return new ClientFacade(business, authorization, requestContext, new FakeClock());
    }

    // --- Valid create ---

    [Fact]
    public async Task CreateAsync_WhenAuthorizedAndValid_ReturnsTheBusinessResultUnchanged()
    {
        var expected = new ClientServiceModel
        {
            Id = Guid.NewGuid(),
            Name = "Acme Corporation",
            LifecycleStatus = ClientLifecycleStatusContract.Lead,
            OwnerUserId = "owner-1",
            CreatedAtUtc = FixedUtcNow,
            CreatedBy = "user-1",
            LastModifiedAtUtc = FixedUtcNow,
            LastModifiedBy = "user-1",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
        };
        var facade = BuildFacade(out var business, out _, businessResult: expected);

        var result = await facade.CreateAsync(CreateRequest(), CancellationToken.None);

        Assert.True(business.WasCalled);
        Assert.Same(expected, result);
    }

    [Fact]
    public async Task CreateAsync_PassesTheRequestAndResolvedActorRequestContextAndClockToBusiness()
    {
        var actor = ActorContext.ForUser("actor-42");
        var facade = BuildFacade(out var business, out _, actor: actor);

        await facade.CreateAsync(CreateRequest(name: "Acme Corporation", ownerUserId: "owner-9"), CancellationToken.None);

        Assert.Equal("Acme Corporation", business.ReceivedRequest?.Name);
        Assert.Equal("owner-9", business.ReceivedRequest?.OwnerUserId);
        Assert.Equal(actor, business.ReceivedActor);
        Assert.Equal(FixedUtcNow, business.ReceivedCreatedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_ChecksAuthorizationForTheResolvedActor()
    {
        var actor = ActorContext.ForUser("actor-7");
        var facade = BuildFacade(out _, out var authorization, actor: actor);

        await facade.CreateAsync(CreateRequest(), CancellationToken.None);

        Assert.True(authorization.WasCalled);
        Assert.Equal(actor, authorization.ReceivedActor);
    }

    // --- Validation failure ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WhenNameIsMissing_ThrowsValidationExceptionAndNeverCallsBusiness(string name)
    {
        var facade = BuildFacade(out var business, out _);

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.CreateAsync(CreateRequest(name: name), CancellationToken.None));

        Assert.False(business.WasCalled);
    }

    [Fact]
    public async Task CreateAsync_WhenOwnerUserIdIsMissing_ThrowsValidationExceptionAndNeverCallsBusiness()
    {
        var facade = BuildFacade(out var business, out _);

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.CreateAsync(CreateRequest(ownerUserId: ""), CancellationToken.None));

        Assert.False(business.WasCalled);
    }

    [Fact]
    public async Task CreateAsync_WhenPrimaryEmailIsNotAValidAddress_ThrowsValidationException()
    {
        var facade = BuildFacade(out var business, out _);

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.CreateAsync(CreateRequest(primaryEmail: "not-an-email"), CancellationToken.None));

        Assert.False(business.WasCalled);
    }

    [Fact]
    public async Task CreateAsync_WhenLifecycleStatusIsUndefined_ThrowsValidationExceptionAndNeverCallsBusiness()
    {
        var facade = BuildFacade(out var business, out _);
        var request = CreateRequest() with { LifecycleStatus = (ClientLifecycleStatusContract)999 };

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.CreateAsync(request, CancellationToken.None));

        Assert.False(business.WasCalled);
    }

    // --- Duplicate warning path (CLIENT-004) ---

    [Fact]
    public async Task CreateAsync_WhenBusinessReturnsPossibleDuplicates_ReturnsThemUnchanged()
    {
        var duplicate = new ClientDuplicateWarning
        {
            ClientId = Guid.NewGuid(),
            Name = "Acme Corporation",
            MatchedOn = [ClientDuplicateMatchField.Name],
        };
        var resultWithDuplicates = new ClientServiceModel
        {
            Id = Guid.NewGuid(),
            Name = "Acme Corporation",
            LifecycleStatus = ClientLifecycleStatusContract.Lead,
            OwnerUserId = "owner-1",
            CreatedAtUtc = FixedUtcNow,
            CreatedBy = "user-1",
            LastModifiedAtUtc = FixedUtcNow,
            LastModifiedBy = "user-1",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            PossibleDuplicates = [duplicate],
        };
        var facade = BuildFacade(out var business, out _, businessResult: resultWithDuplicates);

        var result = await facade.CreateAsync(CreateRequest(), CancellationToken.None);

        // CLIENT-004: creation still succeeds - the warning rides along rather than blocking.
        Assert.True(business.WasCalled);
        var returnedDuplicate = Assert.Single(result.PossibleDuplicates);
        Assert.Equal(duplicate.ClientId, returnedDuplicate.ClientId);
        Assert.Contains(ClientDuplicateMatchField.Name, returnedDuplicate.MatchedOn);
    }

    // --- Unauthorized path (SEC-010..013) ---

    [Fact]
    public async Task CreateAsync_WhenActorIsNotAuthorized_ThrowsUnauthorizedAccessExceptionAndNeverCallsBusiness()
    {
        var facade = BuildFacade(out var business, out var authorization, authorized: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => facade.CreateAsync(CreateRequest(), CancellationToken.None));

        Assert.True(authorization.WasCalled);
        Assert.False(business.WasCalled);
    }

    [Fact]
    public async Task CreateAsync_WhenActorIsNotAuthorized_ChecksAuthorizationBeforeValidatingTheRequest()
    {
        // authorized: false paired with an otherwise-invalid request (blank Name) proves
        // authorization is evaluated first (SEC-013) - an unauthorized caller never learns which
        // fields would have failed validation.
        var facade = BuildFacade(out _, out _, authorized: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => facade.CreateAsync(CreateRequest(name: ""), CancellationToken.None));
    }
}
