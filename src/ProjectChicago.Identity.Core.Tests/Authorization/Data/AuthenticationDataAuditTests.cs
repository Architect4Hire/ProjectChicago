using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProjectChicago.Contracts.Audit;
using ProjectChicago.Identity.Core.Authorization.Data;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;
using ProjectChicago.Identity.Core.Persistence;
using ProjectChicago.Identity.Core.Tests.Persistence;
using IdentityDbContext = ProjectChicago.Identity.Core.Persistence.IdentityDbContext;
using ProjectChicago.Shared.Correlation;
using ProjectChicago.Shared.Messaging;
using ProjectChicago.Shared.Outbox;
using Xunit;

namespace ProjectChicago.Identity.Core.Tests.Authorization.Data;

// Audit event generation tests (SEC-005, AUDIT-001..008, OUTBOX-001..006).
// Verify that authentication security events (login, failed login, account locked, logout) are
// recorded as safe audit events with no credential material, proper correlation/trace context,
// and correct actor type (User/Anonymous/System). Each test confirms exactly one outbox row is
// created with EntityMutationAudited payload, no passwords/tokens/hashes are serialized, and
// the event can be deserialized correctly.
public sealed class AuthenticationDataAuditTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public AuthenticationDataAuditTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    private async Task<(IdentityDbContext, UserManager<ApplicationUser>)> CreateContextAndUserManagerAsync(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = databaseName,
        };

        var dbContextBuilder = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(builder.ConnectionString)
            .Options;

        var context = new IdentityDbContext(dbContextBuilder);
        await context.Database.EnsureCreatedAsync();

        var userStore = new UserStore<ApplicationUser, IdentityRole<Guid>, IdentityDbContext, Guid>(context);
        var userManager = new UserManager<ApplicationUser>(
            userStore,
            null!,
            new PasswordHasher<ApplicationUser>(),
            Enumerable.Empty<IUserValidator<ApplicationUser>>(),
            Enumerable.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            null!
        );

        return (context, userManager);
    }

    [Fact]
    public async Task RecordLoginSuccess_CreatesOneOutboxMessage_WithCorrectAuditEvent()
    {
        // Arrange: Create user and request context
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "testuser",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user);

            var authData = new AuthenticationData(db);
            var requestContext = RequestContext.CreateNew(
                ActorContext.ForUser(user.Id.ToString())
            );

            // Act: Record successful login
            await authData.RecordLoginSuccessAsync(user, requestContext);

            // Assert: One outbox message created
            var outboxMessages = db.OutboxMessages.ToList();
            Assert.Single(outboxMessages);

            var message = outboxMessages[0];
            Assert.Equal(typeof(EntityMutationAudited).FullName, message.ContractType);
            Assert.Equal(EntityMutationAudited.CurrentVersion, message.ContractVersion);
            Assert.Equal(requestContext.CorrelationId, message.CorrelationId);
            Assert.Equal(requestContext.TraceId, message.TraceId);

            // Deserialize payload and verify fields
            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                message.Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            );

            var audit = envelope.Payload;
            Assert.Equal(AuditSourceServices.Identity, audit.SourceService);
            Assert.Equal(AuditEntityTypes.AuthenticationSession, audit.EntityType);
            Assert.Equal(user.Id, audit.EntityId);
            Assert.Equal(AuditActions.LoggedIn, audit.Action);
            Assert.Equal(user.Id.ToString(), audit.ActorId);
            Assert.Equal(AuditActorTypes.User, audit.ActorType);
            Assert.Equal(requestContext.TraceId, audit.TraceId);
            Assert.Equal(requestContext.CorrelationId, audit.CorrelationId);
            Assert.Empty(audit.ChangedFields);
        }
    }

    [Fact]
    public async Task RecordFailedLogin_CreatesOneOutboxMessage_WithAnonymousActor()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var authData = new AuthenticationData(db);
            var requestContext = RequestContext.CreateNew(ActorContext.ForAnonymous());

            // Act: Record failed login
            await authData.RecordFailedLoginAsync("unknown_user", requestContext);

            // Assert: One outbox message created with Anonymous actor
            var outboxMessages = db.OutboxMessages.ToList();
            Assert.Single(outboxMessages);

            var message = outboxMessages[0];
            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                message.Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            );

            var audit = envelope.Payload;
            Assert.Equal(AuditActions.FailedLogin, audit.Action);
            Assert.Null(audit.ActorId);
            Assert.Equal(AuditActorTypes.Anonymous, audit.ActorType);
            Assert.Equal(Guid.Empty, audit.EntityId);
        }
    }

    [Fact]
    public async Task RecordAccountLocked_CreatesOneOutboxMessage_WithSystemActor()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "lockeduser",
                Email = "locked@example.com",
            };
            await userManager.CreateAsync(user);

            var authData = new AuthenticationData(db);
            var requestContext = RequestContext.CreateNew();

            // Act: Record account locked
            await authData.RecordAccountLockedAsync(user, requestContext);

            // Assert: One outbox message with System actor
            var outboxMessages = db.OutboxMessages.ToList();
            Assert.Single(outboxMessages);

            var message = outboxMessages[0];
            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                message.Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            );

            var audit = envelope.Payload;
            Assert.Equal(AuditActions.AccountLocked, audit.Action);
            Assert.Null(audit.ActorId);
            Assert.Equal(AuditActorTypes.System, audit.ActorType);
        }
    }

    [Fact]
    public async Task RecordLogout_CreatesOneOutboxMessage_WithUserActor()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "logoutuser",
                Email = "logout@example.com",
            };
            await userManager.CreateAsync(user);

            var authData = new AuthenticationData(db);
            var requestContext = RequestContext.CreateNew(
                ActorContext.ForUser(user.Id.ToString())
            );

            // Act: Record logout
            await authData.RecordLogoutAsync(user, requestContext);

            // Assert: One outbox message with User actor
            var outboxMessages = db.OutboxMessages.ToList();
            Assert.Single(outboxMessages);

            var message = outboxMessages[0];
            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                message.Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            );

            var audit = envelope.Payload;
            Assert.Equal(AuditActions.LoggedOut, audit.Action);
            Assert.Equal(user.Id.ToString(), audit.ActorId);
            Assert.Equal(AuditActorTypes.User, audit.ActorType);
        }
    }

    [Fact]
    public async Task RecordLoginSuccess_DoesNotIncludePasswordOrSensitiveData()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "testuser",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user);

            var authData = new AuthenticationData(db);
            var requestContext = RequestContext.CreateNew();

            // Act: Record login
            await authData.RecordLoginSuccessAsync(user, requestContext);

            // Assert: Serialized payload contains no password, token, or credential material
            var outboxMessage = db.OutboxMessages.First();
            var payloadJson = outboxMessage.Payload;

            // Check that known forbidden words don't appear in the serialized audit event
            var forbiddenPatterns = new[] { "password", "pwd", "token", "secret", "apikey", "privatekey" };
            foreach (var pattern in forbiddenPatterns)
            {
                Assert.DoesNotContain(pattern, payloadJson, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task RecordFailedLogin_CorrelationAndTraceMetadataPreserved()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var authData = new AuthenticationData(db);
            var requestContext = RequestContext.CreateNew();

            // Act: Record failed login
            await authData.RecordFailedLoginAsync("unknown", requestContext);

            // Assert: Correlation and trace metadata are preserved
            var outboxMessage = db.OutboxMessages.First();
            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                outboxMessage.Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            );

            var audit = envelope.Payload;
            Assert.Equal(requestContext.CorrelationId, audit.CorrelationId);
            Assert.Equal(requestContext.TraceId, audit.TraceId);
            Assert.Equal(requestContext.CausationId, audit.CausationId);
        }
    }

    [Fact]
    public async Task MultipleAuditEvents_EachCreatesSeperateOutboxMessage()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "testuser",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user);

            var authData = new AuthenticationData(db);
            var requestContext = RequestContext.CreateNew();

            // Act: Record multiple events
            await authData.RecordLoginSuccessAsync(user, requestContext);
            await authData.RecordLogoutAsync(user, requestContext);

            // Assert: Two separate outbox messages
            var outboxMessages = db.OutboxMessages.OrderBy(m => m.CreatedAtUtc).ToList();
            Assert.Equal(2, outboxMessages.Count);

            var loginEvent = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                outboxMessages[0].Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            ).Payload;

            var logoutEvent = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                outboxMessages[1].Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            ).Payload;

            Assert.Equal(AuditActions.LoggedIn, loginEvent.Action);
            Assert.Equal(AuditActions.LoggedOut, logoutEvent.Action);
        }
    }

    [Fact]
    public async Task RecordLoginSuccess_EventIdIsUnique()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "testuser",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user);

            var authData = new AuthenticationData(db);
            var requestContext = RequestContext.CreateNew();

            // Act: Record two login events
            await authData.RecordLoginSuccessAsync(user, requestContext);

            // Create new context for second login to avoid tracking conflicts
            var (db2, userManager2) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
            using (db2)
            using (userManager2)
            {
                var authData2 = new AuthenticationData(db2);
                await authData2.RecordLoginSuccessAsync(user, requestContext);

                // Assert: Each event has unique EventId
                var outboxMessages = db2.OutboxMessages.ToList();
                Assert.Equal(2, outboxMessages.Count);

                var event1 = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                    outboxMessages[0].Payload,
                    new[] { EntityMutationAudited.CurrentVersion }
                ).Payload;

                var event2 = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                    outboxMessages[1].Payload,
                    new[] { EntityMutationAudited.CurrentVersion }
                ).Payload;

                Assert.NotEqual(event1.EventId, event2.EventId);
            }
        }
    }
}
