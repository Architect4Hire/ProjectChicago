using System.ComponentModel.DataAnnotations;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Contracts.Common;
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

        public Guid? LifecycleClientIdReceived { get; private set; }

        public ClientLifecycleStatusContract? LifecycleNewStatusReceived { get; private set; }

        public string? LifecycleExpectedConcurrencyTokenReceived { get; private set; }

        public ActorContext? LifecycleActorReceived { get; private set; }

        public RequestContext? LifecycleRequestContextReceived { get; private set; }

        public DateTime? LifecycleChangedAtUtcReceived { get; private set; }

        public bool LifecycleWasCalled { get; private set; }

        public ClientServiceModel? LifecycleResultToReturn { get; set; }

        public Exception? LifecycleExceptionToThrow { get; set; }

        public Task<ClientServiceModel?> ChangeLifecycleStatusAsync(
            Guid clientId,
            ClientLifecycleStatusContract newStatus,
            string expectedConcurrencyToken,
            ActorContext actor,
            RequestContext requestContext,
            DateTime changedAtUtc,
            CancellationToken cancellationToken)
        {
            LifecycleWasCalled = true;
            LifecycleClientIdReceived = clientId;
            LifecycleNewStatusReceived = newStatus;
            LifecycleExpectedConcurrencyTokenReceived = expectedConcurrencyToken;
            LifecycleActorReceived = actor;
            LifecycleRequestContextReceived = requestContext;
            LifecycleChangedAtUtcReceived = changedAtUtc;

            if (LifecycleExceptionToThrow is not null)
            {
                throw LifecycleExceptionToThrow;
            }

            return Task.FromResult(LifecycleResultToReturn);
        }

        public ListClientsRequest? ReceivedListRequest { get; private set; }

        public bool ListWasCalled { get; private set; }

        public PagedResponse<ClientServiceModel> ListResultToReturn { get; set; } = BuildDefaultListResult();

        public Task<PagedResponse<ClientServiceModel>> ListAsync(
            ListClientsRequest request,
            CancellationToken cancellationToken)
        {
            ListWasCalled = true;
            ReceivedListRequest = request;
            return Task.FromResult(ListResultToReturn);
        }

        public Guid? DetailClientIdReceived { get; private set; }

        public bool DetailWasCalled { get; private set; }

        public ClientDetailServiceModel? DetailResultToReturn { get; set; }

        public Task<ClientDetailServiceModel?> GetDetailAsync(Guid clientId, CancellationToken cancellationToken)
        {
            DetailWasCalled = true;
            DetailClientIdReceived = clientId;
            return Task.FromResult(DetailResultToReturn);
        }

        private static PagedResponse<ClientServiceModel> BuildDefaultListResult() => new()
        {
            Items = [],
            Page = 1,
            PageSize = 25,
            TotalCount = 0,
            TotalPages = 0,
        };

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

        public ActorContext? ReceivedListActor { get; private set; }

        public bool ListWasCalled { get; private set; }

        public Task<bool> CanCreateAsync(ActorContext actor, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedActor = actor;
            return Task.FromResult(AuthorizedResult);
        }

        public Task<bool> CanListAsync(ActorContext actor, CancellationToken cancellationToken)
        {
            ListWasCalled = true;
            ReceivedListActor = actor;
            return Task.FromResult(AuthorizedResult);
        }

        public ActorContext? ReceivedDetailActor { get; private set; }

        public bool DetailWasCalled { get; private set; }

        public Task<bool> CanGetDetailAsync(ActorContext actor, CancellationToken cancellationToken)
        {
            DetailWasCalled = true;
            ReceivedDetailActor = actor;
            return Task.FromResult(AuthorizedResult);
        }

        public ActorContext? ReceivedLifecycleActor { get; private set; }

        public bool LifecycleWasCalled { get; private set; }

        public Task<bool> CanChangeLifecycleStatusAsync(ActorContext actor, CancellationToken cancellationToken)
        {
            LifecycleWasCalled = true;
            ReceivedLifecycleActor = actor;
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

    // --- List/search (CLIENT-020..024, SEC-010..013) ---

    private static ListClientsRequest CreateListRequest(
        string? search = null,
        ClientLifecycleStatusContract? lifecycleStatus = null,
        string? ownerUserId = null,
        bool? isActive = null,
        ClientSortField? sortBy = null,
        ClientSortDirection? sortDirection = null,
        int page = 1,
        int pageSize = 25) => new()
    {
        Search = search,
        LifecycleStatus = lifecycleStatus,
        OwnerUserId = ownerUserId,
        IsActive = isActive,
        SortBy = sortBy,
        SortDirection = sortDirection,
        Page = page,
        PageSize = pageSize,
    };

    [Fact]
    public async Task ListAsync_WhenAuthorizedAndValid_ReturnsTheBusinessResultUnchanged()
    {
        var expected = new PagedResponse<ClientServiceModel>
        {
            Items = [],
            Page = 2,
            PageSize = 10,
            TotalCount = 0,
            TotalPages = 0,
        };
        var facade = BuildFacade(out var business, out _);
        business.ListResultToReturn = expected;

        var result = await facade.ListAsync(CreateListRequest(page: 2, pageSize: 10), CancellationToken.None);

        Assert.True(business.ListWasCalled);
        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ListAsync_PassesTheUntouchedRequestToBusiness()
    {
        var facade = BuildFacade(out var business, out _);
        var request = CreateListRequest(search: "acme", page: 3, pageSize: 10);

        await facade.ListAsync(request, CancellationToken.None);

        Assert.Same(request, business.ReceivedListRequest);
    }

    [Fact]
    public async Task ListAsync_ChecksAuthorizationForTheResolvedActor()
    {
        var actor = ActorContext.ForUser("actor-7");
        var facade = BuildFacade(out _, out var authorization, actor: actor);

        await facade.ListAsync(CreateListRequest(), CancellationToken.None);

        Assert.True(authorization.ListWasCalled);
        Assert.Equal(actor, authorization.ReceivedListActor);
    }

    [Fact]
    public async Task ListAsync_WhenActorIsNotAuthorized_ThrowsUnauthorizedAccessExceptionAndNeverCallsBusiness()
    {
        var facade = BuildFacade(out var business, out var authorization, authorized: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => facade.ListAsync(CreateListRequest(), CancellationToken.None));

        Assert.True(authorization.ListWasCalled);
        Assert.False(business.ListWasCalled);
    }

    [Fact]
    public async Task ListAsync_WhenActorIsNotAuthorized_ChecksAuthorizationBeforeValidatingTheRequest()
    {
        // authorized: false paired with an otherwise-invalid request (Page 0) proves authorization
        // is evaluated first (SEC-013) - an unauthorized caller never learns which paging value
        // would have failed validation.
        var facade = BuildFacade(out _, out _, authorized: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => facade.ListAsync(CreateListRequest(page: 0), CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ListAsync_WhenPageIsOutOfRange_ThrowsValidationExceptionAndNeverCallsBusiness(int page)
    {
        var facade = BuildFacade(out var business, out _);

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.ListAsync(CreateListRequest(page: page), CancellationToken.None));

        Assert.False(business.ListWasCalled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ListAsync_WhenPageSizeIsOutOfRange_ThrowsValidationExceptionAndNeverCallsBusiness(int pageSize)
    {
        var facade = BuildFacade(out var business, out _);

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.ListAsync(CreateListRequest(pageSize: pageSize), CancellationToken.None));

        Assert.False(business.ListWasCalled);
    }

    [Fact]
    public async Task ListAsync_WhenLifecycleStatusIsUndefined_ThrowsValidationExceptionAndNeverCallsBusiness()
    {
        var facade = BuildFacade(out var business, out _);
        var request = CreateListRequest() with { LifecycleStatus = (ClientLifecycleStatusContract)999 };

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.ListAsync(request, CancellationToken.None));

        Assert.False(business.ListWasCalled);
    }

    [Fact]
    public async Task ListAsync_WhenSortByIsUndefined_ThrowsValidationExceptionAndNeverCallsBusiness()
    {
        var facade = BuildFacade(out var business, out _);
        var request = CreateListRequest() with { SortBy = (ClientSortField)999 };

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.ListAsync(request, CancellationToken.None));

        Assert.False(business.ListWasCalled);
    }

    [Fact]
    public async Task ListAsync_WhenSortDirectionIsUndefined_ThrowsValidationExceptionAndNeverCallsBusiness()
    {
        var facade = BuildFacade(out var business, out _);
        var request = CreateListRequest() with { SortDirection = (ClientSortDirection)999 };

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.ListAsync(request, CancellationToken.None));

        Assert.False(business.ListWasCalled);
    }

    [Fact]
    public async Task ListAsync_WhenSearchExceedsMaxLength_ThrowsValidationExceptionAndNeverCallsBusiness()
    {
        var facade = BuildFacade(out var business, out _);
        var request = CreateListRequest(search: new string('a', 201));

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.ListAsync(request, CancellationToken.None));

        Assert.False(business.ListWasCalled);
    }

    // --- GetDetailAsync (CLIENT-030..032, SEC-010..013) ---

    [Fact]
    public async Task GetDetailAsync_WhenAuthorized_ReturnsTheBusinessResultUnchanged()
    {
        var expected = new ClientDetailServiceModel
        {
            Client = new ClientServiceModel
            {
                Id = Guid.NewGuid(),
                Name = "Acme Corporation",
                LifecycleStatus = ClientLifecycleStatusContract.Active,
                OwnerUserId = "owner-1",
                CreatedAtUtc = FixedUtcNow,
                CreatedBy = "user-1",
                LastModifiedAtUtc = FixedUtcNow,
                LastModifiedBy = "user-1",
                ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
            },
            ActiveProjects = [],
            HistoricalProjects = [],
            OpenTasks = [],
            RecentlyCompletedTasks = [],
        };
        var facade = BuildFacade(out var business, out _);
        business.DetailResultToReturn = expected;

        var result = await facade.GetDetailAsync(expected.Client.Id, CancellationToken.None);

        Assert.True(business.DetailWasCalled);
        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetDetailAsync_PassesTheRequestedClientIdToBusiness()
    {
        var facade = BuildFacade(out var business, out _);
        var clientId = Guid.NewGuid();

        await facade.GetDetailAsync(clientId, CancellationToken.None);

        Assert.Equal(clientId, business.DetailClientIdReceived);
    }

    [Fact]
    public async Task GetDetailAsync_WhenBusinessReturnsNull_ReturnsNull()
    {
        var facade = BuildFacade(out var business, out _);
        business.DetailResultToReturn = null;

        var result = await facade.GetDetailAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDetailAsync_ChecksAuthorizationForTheResolvedActor()
    {
        var actor = ActorContext.ForUser("actor-7");
        var facade = BuildFacade(out _, out var authorization, actor: actor);

        await facade.GetDetailAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(authorization.DetailWasCalled);
        Assert.Equal(actor, authorization.ReceivedDetailActor);
    }

    [Fact]
    public async Task GetDetailAsync_WhenClientIdIsEmpty_ThrowsValidationExceptionAndNeverChecksAuthorization()
    {
        var facade = BuildFacade(out var business, out var authorization);

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.GetDetailAsync(Guid.Empty, CancellationToken.None));

        Assert.False(authorization.DetailWasCalled);
        Assert.False(business.DetailWasCalled);
    }

    [Fact]
    public async Task GetDetailAsync_WhenActorIsNotAuthorized_ThrowsUnauthorizedAccessExceptionAndNeverCallsBusiness()
    {
        var facade = BuildFacade(out var business, out var authorization, authorized: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => facade.GetDetailAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.True(authorization.DetailWasCalled);
        Assert.False(business.DetailWasCalled);
    }

    // --- ChangeLifecycleStatusAsync (CLIENT-010..015, SEC-010..013, DATA-008) ---

    private static ChangeClientLifecycleStatusViewModel CreateLifecycleRequest(
        ClientLifecycleStatusContract newStatus = ClientLifecycleStatusContract.Active,
        string expectedConcurrencyToken = "dGVzdA==") => new()
        {
            NewStatus = newStatus,
            ExpectedConcurrencyToken = expectedConcurrencyToken,
        };

    [Fact]
    public async Task ChangeLifecycleStatusAsync_WhenAuthorizedAndValid_ReturnsTheBusinessResultUnchanged()
    {
        var expected = new ClientServiceModel
        {
            Id = Guid.NewGuid(),
            Name = "Acme Corporation",
            LifecycleStatus = ClientLifecycleStatusContract.Active,
            OwnerUserId = "owner-1",
            CreatedAtUtc = FixedUtcNow,
            CreatedBy = "user-1",
            LastModifiedAtUtc = FixedUtcNow,
            LastModifiedBy = "user-1",
            ConcurrencyToken = Convert.ToBase64String([9, 9, 9, 9, 9, 9, 9, 9]),
        };
        var facade = BuildFacade(out var business, out _);
        business.LifecycleResultToReturn = expected;

        var result = await facade.ChangeLifecycleStatusAsync(
            Guid.NewGuid(), CreateLifecycleRequest(), CancellationToken.None);

        Assert.True(business.LifecycleWasCalled);
        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ChangeLifecycleStatusAsync_PassesTheClientIdRequestFieldsAndResolvedActorRequestContextAndClockToBusiness()
    {
        var actor = ActorContext.ForUser("actor-42");
        var facade = BuildFacade(out var business, out _, actor: actor);
        var clientId = Guid.NewGuid();
        var request = CreateLifecycleRequest(ClientLifecycleStatusContract.OnHold, "c29tZS10b2tlbg==");

        await facade.ChangeLifecycleStatusAsync(clientId, request, CancellationToken.None);

        Assert.Equal(clientId, business.LifecycleClientIdReceived);
        Assert.Equal(ClientLifecycleStatusContract.OnHold, business.LifecycleNewStatusReceived);
        Assert.Equal("c29tZS10b2tlbg==", business.LifecycleExpectedConcurrencyTokenReceived);
        Assert.Equal(actor, business.LifecycleActorReceived);
        Assert.Equal(FixedUtcNow, business.LifecycleChangedAtUtcReceived);
    }

    [Fact]
    public async Task ChangeLifecycleStatusAsync_ChecksAuthorizationForTheResolvedActor()
    {
        var actor = ActorContext.ForUser("actor-7");
        var facade = BuildFacade(out _, out var authorization, actor: actor);

        await facade.ChangeLifecycleStatusAsync(Guid.NewGuid(), CreateLifecycleRequest(), CancellationToken.None);

        Assert.True(authorization.LifecycleWasCalled);
        Assert.Equal(actor, authorization.ReceivedLifecycleActor);
    }

    [Fact]
    public async Task ChangeLifecycleStatusAsync_WhenBusinessReturnsNull_ReturnsNull()
    {
        var facade = BuildFacade(out var business, out _);
        business.LifecycleResultToReturn = null;

        var result = await facade.ChangeLifecycleStatusAsync(Guid.NewGuid(), CreateLifecycleRequest(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ChangeLifecycleStatusAsync_WhenClientIdIsEmpty_ThrowsValidationExceptionAndNeverChecksAuthorization()
    {
        var facade = BuildFacade(out var business, out var authorization);

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.ChangeLifecycleStatusAsync(Guid.Empty, CreateLifecycleRequest(), CancellationToken.None));

        Assert.False(authorization.LifecycleWasCalled);
        Assert.False(business.LifecycleWasCalled);
    }

    [Fact]
    public async Task ChangeLifecycleStatusAsync_WhenNewStatusIsUndefined_ThrowsValidationExceptionAndNeverCallsBusiness()
    {
        var facade = BuildFacade(out var business, out _);
        var request = CreateLifecycleRequest() with { NewStatus = (ClientLifecycleStatusContract)999 };

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.ChangeLifecycleStatusAsync(Guid.NewGuid(), request, CancellationToken.None));

        Assert.False(business.LifecycleWasCalled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ChangeLifecycleStatusAsync_WhenExpectedConcurrencyTokenIsMissing_ThrowsValidationExceptionAndNeverCallsBusiness(
        string token)
    {
        var facade = BuildFacade(out var business, out _);
        var request = CreateLifecycleRequest(expectedConcurrencyToken: token);

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.ChangeLifecycleStatusAsync(Guid.NewGuid(), request, CancellationToken.None));

        Assert.False(business.LifecycleWasCalled);
    }

    [Fact]
    public async Task ChangeLifecycleStatusAsync_WhenActorIsNotAuthorized_ThrowsUnauthorizedAccessExceptionAndNeverCallsBusiness()
    {
        var facade = BuildFacade(out var business, out var authorization, authorized: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => facade.ChangeLifecycleStatusAsync(Guid.NewGuid(), CreateLifecycleRequest(), CancellationToken.None));

        Assert.True(authorization.LifecycleWasCalled);
        Assert.False(business.LifecycleWasCalled);
    }

    [Fact]
    public async Task ChangeLifecycleStatusAsync_WhenActorIsNotAuthorized_ChecksAuthorizationBeforeValidatingTheRequest()
    {
        // authorized: false paired with an otherwise-invalid request (undefined NewStatus) proves
        // authorization is evaluated first (SEC-013).
        var facade = BuildFacade(out _, out _, authorized: false);
        var request = CreateLifecycleRequest() with { NewStatus = (ClientLifecycleStatusContract)999 };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => facade.ChangeLifecycleStatusAsync(Guid.NewGuid(), request, CancellationToken.None));
    }

    [Fact]
    public async Task ChangeLifecycleStatusAsync_WhenBusinessThrowsInvalidOperationException_PropagatesItUnchanged()
    {
        var facade = BuildFacade(out var business, out _);
        business.LifecycleExceptionToThrow = new InvalidOperationException("Transition not allowed.");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => facade.ChangeLifecycleStatusAsync(Guid.NewGuid(), CreateLifecycleRequest(), CancellationToken.None));
    }
}
