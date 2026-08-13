using System.ComponentModel.DataAnnotations;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Projects;
using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Crm.Core.Facades;
using ProjectChicago.Shared.Correlation;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Facades;

// Pure unit tests for ProjectFacade (PROJECT-001..002, SEC-010..013; backend.md Tests: "Unit-test
// Facade/Business/Data behavior at the layer that owns the rule"). IProjectBusiness,
// IProjectAuthorization, ICurrentRequestContext, and IClock are faked rather than backed by real
// infrastructure - proving the Facade's own authorization-call/validation/orchestration rules do
// not require EF, HTTP, or a real clock (RESTRICTION: this scope never touches Data/EF).
// CreateAsync's contract is "authorize, validate, resolve context, delegate, return unchanged" -
// the ViewModel<->ServiceModel mapping itself belongs to IProjectBusiness/ProjectBusinessTests,
// not here.
public class ProjectFacadeTests
{
    private sealed class FakeProjectBusiness : IProjectBusiness
    {
        public CreateProjectViewModel? ReceivedRequest { get; private set; }

        public ActorContext? ReceivedActor { get; private set; }

        public RequestContext? ReceivedRequestContext { get; private set; }

        public DateTime? ReceivedCreatedAtUtc { get; private set; }

        public bool WasCalled { get; private set; }

        public ProjectServiceModel ResultToReturn { get; init; } = BuildDefaultResult();

        public Task<ProjectServiceModel> CreateAsync(
            CreateProjectViewModel request,
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

        public Task<PagedResponse<ProjectServiceModel>> ListAsync(
            ListProjectsRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<ProjectServiceModel>
            {
                Items = [],
                Page = 1,
                PageSize = 25,
                TotalCount = 0,
                TotalPages = 0,
            });

        public Task<ProjectDetailServiceModel?> GetDetailAsync(
            Guid projectId,
            CancellationToken cancellationToken) =>
            Task.FromResult<ProjectDetailServiceModel?>(null);

        public Task<ProjectServiceModel?> TransitionStatusAsync(
            Guid projectId,
            ProjectStatusContract targetStatus,
            string expectedConcurrencyToken,
            ActorContext actor,
            RequestContext requestContext,
            DateTime transitionedAtUtc,
            bool acknowledgeOpenTasks = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectServiceModel?>(ResultToReturn);

        public Task<ProjectServiceModel?> ArchiveAsync(
            Guid projectId,
            string expectedConcurrencyToken,
            ActorContext actor,
            RequestContext requestContext,
            DateTime archivedAtUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult<ProjectServiceModel?>(ResultToReturn);

        private static ProjectServiceModel BuildDefaultResult() => new()
        {
            Id = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            Name = "Website Redesign",
            Status = ProjectStatusContract.Planned,
            Priority = ProjectPriorityContract.Normal,
            OwnerUserId = "owner-1",
            CreatedAtUtc = FixedUtcNow,
            CreatedBy = "user-1",
            LastModifiedAtUtc = FixedUtcNow,
            LastModifiedBy = "user-1",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
        };
    }

    private sealed class FakeProjectAuthorization : IProjectAuthorization
    {
        public ActorContext? ReceivedActor { get; private set; }

        public Guid? ReceivedClientId { get; private set; }

        public bool WasCalled { get; private set; }

        public bool AuthorizedResult { get; init; } = true;

        public Task<bool> CanCreateAsync(ActorContext actor, Guid clientId, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedActor = actor;
            ReceivedClientId = clientId;
            return Task.FromResult(AuthorizedResult);
        }

        public Task<bool> CanListAsync(ActorContext actor, CancellationToken cancellationToken) =>
            Task.FromResult(AuthorizedResult);
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

    private static CreateProjectViewModel CreateRequest(
        Guid? clientId = null,
        string name = "Website Redesign",
        ProjectStatusContract? status = null,
        ProjectPriorityContract? priority = null,
        string ownerUserId = "owner-1") => new()
    {
        ClientId = clientId ?? Guid.NewGuid(),
        Name = name,
        Status = status,
        Priority = priority,
        OwnerUserId = ownerUserId,
    };

    private static ProjectFacade BuildFacade(
        out FakeProjectBusiness business,
        out FakeProjectAuthorization authorization,
        bool authorized = true,
        ActorContext? actor = null,
        ProjectServiceModel? businessResult = null)
    {
        business = businessResult is null
            ? new FakeProjectBusiness()
            : new FakeProjectBusiness { ResultToReturn = businessResult };
        authorization = new FakeProjectAuthorization { AuthorizedResult = authorized };

        var requestContext = new FakeCurrentRequestContext
        {
            Current = RequestContext.CreateNew(actor ?? ActorContext.ForUser("user-1")),
        };

        return new ProjectFacade(business, authorization, requestContext, new FakeClock());
    }

    // --- Valid create ---

    [Fact]
    public async Task CreateAsync_WhenAuthorizedAndValid_ReturnsTheBusinessResultUnchanged()
    {
        var clientId = Guid.NewGuid();
        var expected = new ProjectServiceModel
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Website Redesign",
            Status = ProjectStatusContract.Planned,
            Priority = ProjectPriorityContract.Normal,
            OwnerUserId = "owner-1",
            CreatedAtUtc = FixedUtcNow,
            CreatedBy = "user-1",
            LastModifiedAtUtc = FixedUtcNow,
            LastModifiedBy = "user-1",
            ConcurrencyToken = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
        };
        var facade = BuildFacade(out var business, out _, businessResult: expected);

        var result = await facade.CreateAsync(CreateRequest(clientId: clientId), CancellationToken.None);

        Assert.True(business.WasCalled);
        Assert.Same(expected, result);
    }

    [Fact]
    public async Task CreateAsync_PassesTheRequestAndResolvedActorRequestContextAndClockToBusiness()
    {
        var clientId = Guid.NewGuid();
        var actor = ActorContext.ForUser("actor-42");
        var facade = BuildFacade(out var business, out _, actor: actor);

        await facade.CreateAsync(CreateRequest(clientId: clientId, name: "Website Redesign", ownerUserId: "owner-9"), CancellationToken.None);

        Assert.Equal(clientId, business.ReceivedRequest?.ClientId);
        Assert.Equal("Website Redesign", business.ReceivedRequest?.Name);
        Assert.Equal("owner-9", business.ReceivedRequest?.OwnerUserId);
        Assert.Equal(actor, business.ReceivedActor);
        Assert.Equal(FixedUtcNow, business.ReceivedCreatedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_ChecksAuthorizationForTheResolvedActorAndClientScope()
    {
        var clientId = Guid.NewGuid();
        var actor = ActorContext.ForUser("actor-7");
        var facade = BuildFacade(out _, out var authorization, actor: actor);

        await facade.CreateAsync(CreateRequest(clientId: clientId), CancellationToken.None);

        Assert.True(authorization.WasCalled);
        Assert.Equal(actor, authorization.ReceivedActor);
        Assert.Equal(clientId, authorization.ReceivedClientId);
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
    public async Task CreateAsync_WhenStatusIsUndefined_ThrowsValidationExceptionAndNeverCallsBusiness()
    {
        var facade = BuildFacade(out var business, out _);
        var request = CreateRequest() with { Status = (ProjectStatusContract)999 };

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.CreateAsync(request, CancellationToken.None));

        Assert.False(business.WasCalled);
    }

    [Fact]
    public async Task CreateAsync_WhenPriorityIsUndefined_ThrowsValidationExceptionAndNeverCallsBusiness()
    {
        var facade = BuildFacade(out var business, out _);
        var request = CreateRequest() with { Priority = (ProjectPriorityContract)999 };

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.CreateAsync(request, CancellationToken.None));

        Assert.False(business.WasCalled);
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

    [Fact]
    public async Task CreateAsync_WhenActorIsNotAuthorizedForTheClient_ThrowsUnauthorizedAccessException()
    {
        // PROJECT-001: authorization is scoped to the Client - the actor must be authorized for
        // the specific Client to which the Project is being added.
        var clientId = Guid.NewGuid();
        var facade = BuildFacade(out _, out _, authorized: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => facade.CreateAsync(CreateRequest(clientId: clientId), CancellationToken.None));
    }
}
