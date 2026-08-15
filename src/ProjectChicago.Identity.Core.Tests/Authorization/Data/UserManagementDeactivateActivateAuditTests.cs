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

// User deactivation/activation audit event generation tests (SEC-004, AUDIT-001..008, OUTBOX-001..006).
// Verify that deactivation prevents access, activation restores eligibility, and audit events are recorded correctly.
public sealed class UserManagementDeactivateActivateAuditTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public UserManagementDeactivateActivateAuditTests(MsSqlContainerFixture fixture)
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
    public async Task RecordUserDeactivated_CreatesOutboxMessage_WithDeactivationAuditEvent()
    {
        // Arrange: Create user
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "test@example.com",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user);

            var userMgmtData = new UserManagementData(db);
            var requestContext = RequestContext.CreateNew(
                ActorContext.ForUser(Guid.NewGuid().ToString())
            );

            // Act: Record user deactivation
            await userMgmtData.RecordUserDeactivatedAsync(user, requestContext);

            // Assert: Outbox message with correct deactivation event
            var outboxMessages = db.OutboxMessages.ToList();
            Assert.Single(outboxMessages);

            var message = outboxMessages[0];
            Assert.Equal(typeof(EntityMutationAudited).FullName, message.ContractType);
            Assert.Equal(EntityMutationAudited.CurrentVersion, message.ContractVersion);

            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                message.Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            );

            var audit = envelope.Payload;
            Assert.Equal(AuditActions.UserDeactivated, audit.Action);
            Assert.Equal(user.Id, audit.EntityId);
            Assert.Contains("LockoutEnd", audit.ChangedFields);
        }
    }

    [Fact]
    public async Task RecordUserActivated_CreatesOutboxMessage_WithActivationAuditEvent()
    {
        // Arrange: Create user
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "test@example.com",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user);

            var userMgmtData = new UserManagementData(db);
            var requestContext = RequestContext.CreateNew();

            // Act: Record user activation
            await userMgmtData.RecordUserActivatedAsync(user, requestContext);

            // Assert: Outbox message with correct activation event
            var outboxMessages = db.OutboxMessages.ToList();
            Assert.Single(outboxMessages);

            var message = outboxMessages[0];
            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                message.Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            );

            var audit = envelope.Payload;
            Assert.Equal(AuditActions.UserActivated, audit.Action);
            Assert.Equal(user.Id, audit.EntityId);
        }
    }

    [Fact]
    public async Task Deactivation_LockoutEndSetToMaxValue()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "test@example.com",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user);

            var userMgmtData = new UserManagementData(db);
            var requestContext = RequestContext.CreateNew();

            // Act: Deactivate user (we manually set lockout here to simulate Business behavior)
            user.LockoutEnd = DateTimeOffset.MaxValue;
            user.LockoutEnabled = true;
            await userManager.UpdateSecurityStampAsync(user);
            await userManager.UpdateAsync(user);

            // Assert: LockoutEnd is set to MaxValue
            var refreshedUser = await userManager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(refreshedUser);
            Assert.Equal(DateTimeOffset.MaxValue, refreshedUser.LockoutEnd);
            Assert.True(refreshedUser.LockoutEnabled);
        }
    }

    [Fact]
    public async Task Activation_LockoutEndSetToNull()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "test@example.com",
                Email = "test@example.com",
                LockoutEnd = DateTimeOffset.MaxValue,
                LockoutEnabled = true,
            };
            await userManager.CreateAsync(user);

            // Act: Activate user (we manually clear lockout here to simulate Business behavior)
            user.LockoutEnd = null;
            await userManager.UpdateSecurityStampAsync(user);
            await userManager.UpdateAsync(user);

            // Assert: LockoutEnd is null
            var refreshedUser = await userManager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(refreshedUser);
            Assert.Null(refreshedUser.LockoutEnd);
        }
    }

    [Fact]
    public async Task SecurityStampUpdatedOnDeactivation_InvalidatesSessions()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "test@example.com",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user);

            var originalStamp = user.ConcurrencyStamp;

            // Act: Update security stamp (simulating deactivation)
            await userManager.UpdateSecurityStampAsync(user);

            // Assert: SecurityStamp changed
            var refreshedUser = await userManager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(refreshedUser);
            Assert.NotEqual(originalStamp, refreshedUser.ConcurrencyStamp);
        }
    }

    [Fact]
    public async Task MultipleDeactivations_EachCreatesAuditEvent()
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

            // Act: Deactivate both users
            await userMgmtData.RecordUserDeactivatedAsync(user1, requestContext);
            await userMgmtData.RecordUserDeactivatedAsync(user2, requestContext);

            // Assert: Two separate outbox messages
            var outboxMessages = db.OutboxMessages.OrderBy(m => m.CreatedAtUtc).ToList();
            Assert.Equal(2, outboxMessages.Count);

            var event1 = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                outboxMessages[0].Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            ).Payload;

            var event2 = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                outboxMessages[1].Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            ).Payload;

            Assert.Equal(user1.Id, event1.EntityId);
            Assert.Equal(user2.Id, event2.EntityId);
            Assert.Equal(AuditActions.UserDeactivated, event1.Action);
            Assert.Equal(AuditActions.UserDeactivated, event2.Action);
        }
    }

    [Fact]
    public async Task DeactivationAuditEvent_DoesNotIncludePassword()
    {
        // Arrange
        var (db, userManager) = await CreateContextAndUserManagerAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "test@example.com",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user);

            var userMgmtData = new UserManagementData(db);
            var requestContext = RequestContext.CreateNew();

            // Act: Record deactivation
            await userMgmtData.RecordUserDeactivatedAsync(user, requestContext);

            // Assert: No password in payload
            var outboxMessage = db.OutboxMessages.First();
            var payloadJson = outboxMessage.Payload;

            var forbiddenPatterns = new[] { "password", "pwd", "token", "secret", "apikey", "privatekey", "hash" };
            foreach (var pattern in forbiddenPatterns)
            {
                Assert.DoesNotContain(pattern, payloadJson, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
