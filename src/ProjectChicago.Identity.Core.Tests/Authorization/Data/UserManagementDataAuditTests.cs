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

// User creation audit event generation tests (SEC-004, AUDIT-001..008, OUTBOX-001..006).
// Verify that user creation events are recorded as safe audit events with no credential material,
// proper correlation/trace context, and correct actor type. Each test confirms exactly one outbox row
// is created with EntityMutationAudited payload, no passwords/hashes/credentials are serialized,
// and the event can be deserialized correctly.
public sealed class UserManagementDataAuditTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public UserManagementDataAuditTests(MsSqlContainerFixture fixture)
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
    public async Task RecordUserCreated_CreatesOneOutboxMessage_WithCorrectAuditEvent()
    {
        // Arrange: Create user and request context
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "testuser@example.com",
                Email = "testuser@example.com",
            };
            await userManager.CreateAsync(user);

            var userMgmtData = new UserManagementData(db);
            var requestContext = RequestContext.CreateNew(
                ActorContext.ForUser(Guid.NewGuid().ToString())
            );

            // Act: Record user creation
            await userMgmtData.RecordUserCreatedAsync(user, "Administrator", requestContext);

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
            Assert.Equal(AuditEntityTypes.ApplicationUser, audit.EntityType);
            Assert.Equal(user.Id, audit.EntityId);
            Assert.Equal(AuditActions.UserCreated, audit.Action);
            Assert.NotNull(audit.ActorId);
            Assert.Equal(AuditActorTypes.User, audit.ActorType);
            Assert.Equal(requestContext.TraceId, audit.TraceId);
            Assert.Equal(requestContext.CorrelationId, audit.CorrelationId);
            Assert.Contains("Email", audit.ChangedFields);
            Assert.Contains("RoleName", audit.ChangedFields);
            Assert.NotNull(audit.NewValues);
            Assert.Contains("Administrator", audit.NewValues.Values);
        }
    }

    [Fact]
    public async Task RecordUserCreated_DoesNotIncludePasswordOrSensitiveData()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "testuser@example.com",
                Email = "testuser@example.com",
            };
            await userManager.CreateAsync(user);

            var userMgmtData = new UserManagementData(db);
            var requestContext = RequestContext.CreateNew();

            // Act: Record user creation
            await userMgmtData.RecordUserCreatedAsync(user, "Manager", requestContext);

            // Assert: Serialized payload contains no password, token, or credential material
            var outboxMessage = db.OutboxMessages.First();
            var payloadJson = outboxMessage.Payload;

            // Check that known forbidden words don't appear in the serialized audit event
            var forbiddenPatterns = new[] { "password", "pwd", "token", "secret", "apikey", "privatekey", "hash" };
            foreach (var pattern in forbiddenPatterns)
            {
                Assert.DoesNotContain(pattern, payloadJson, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task RecordUserCreated_CorrelationAndTraceMetadataPreserved()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "testuser@example.com",
                Email = "testuser@example.com",
            };
            await userManager.CreateAsync(user);

            var userMgmtData = new UserManagementData(db);
            var requestContext = RequestContext.CreateNew();

            // Act: Record user creation
            await userMgmtData.RecordUserCreatedAsync(user, "Contributor", requestContext);

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
    public async Task MultipleUserCreations_EachCreatesSeperateOutboxMessage()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user1 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "user1@example.com",
                Email = "user1@example.com",
            };
            var user2 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "user2@example.com",
                Email = "user2@example.com",
            };
            await userManager.CreateAsync(user1);
            await userManager.CreateAsync(user2);

            var userMgmtData = new UserManagementData(db);
            var requestContext = RequestContext.CreateNew();

            // Act: Record multiple user creations
            await userMgmtData.RecordUserCreatedAsync(user1, "Administrator", requestContext);
            await userMgmtData.RecordUserCreatedAsync(user2, "Manager", requestContext);

            // Assert: Two separate outbox messages
            var outboxMessages = db.OutboxMessages.OrderBy(m => m.CreatedAtUtc).ToList();
            Assert.Equal(2, outboxMessages.Count);

            var user1Event = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                outboxMessages[0].Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            ).Payload;

            var user2Event = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                outboxMessages[1].Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            ).Payload;

            Assert.Equal(user1.Id, user1Event.EntityId);
            Assert.Equal(user2.Id, user2Event.EntityId);
            Assert.NotNull(user1Event.NewValues);
            Assert.NotNull(user2Event.NewValues);
            Assert.Equal("Administrator", user1Event.NewValues!["RoleName"]);
            Assert.Equal("Manager", user2Event.NewValues!["RoleName"]);
        }
    }

    [Fact]
    public async Task RecordUserCreated_EventIdIsUnique()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "testuser@example.com",
                Email = "testuser@example.com",
            };
            await userManager.CreateAsync(user);

            var userMgmtData = new UserManagementData(db);
            var requestContext = RequestContext.CreateNew();

            // Act: Record same user creation twice (in separate DB contexts to avoid tracking issues)
            await userMgmtData.RecordUserCreatedAsync(user, "Administrator", requestContext);

            var (db2, userManager2) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
            using (db2)
            using (userManager2)
            {
                var user2 = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user2@example.com", Email = "user2@example.com" };
                await userManager2.CreateAsync(user2);

                var userMgmtData2 = new UserManagementData(db2);
                await userMgmtData2.RecordUserCreatedAsync(user2, "Administrator", requestContext);

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

    [Fact]
    public async Task RecordUserCreated_RoleNameIncludedInAuditEvent()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "testuser@example.com",
                Email = "testuser@example.com",
            };
            await userManager.CreateAsync(user);

            var userMgmtData = new UserManagementData(db);
            var requestContext = RequestContext.CreateNew();
            var roleName = "ReadOnly";

            // Act: Record user creation with specific role
            await userMgmtData.RecordUserCreatedAsync(user, roleName, requestContext);

            // Assert: Role name is captured in audit event
            var outboxMessage = db.OutboxMessages.First();
            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                outboxMessage.Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            );

            var audit = envelope.Payload;
            Assert.NotNull(audit.NewValues);
            Assert.Equal(roleName, audit.NewValues["RoleName"]);
        }
    }
}
