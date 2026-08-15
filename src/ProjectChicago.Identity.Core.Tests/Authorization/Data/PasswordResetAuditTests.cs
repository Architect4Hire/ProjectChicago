using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProjectChicago.Contracts.Audit;
using ProjectChicago.Identity.Core.Authorization.Business;
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

// Password reset audit event generation tests (SEC-004, SEC-005, AUDIT-001..008, OUTBOX-001..006).
// Verify that password reset events (initiation and completion) are recorded without exposing tokens or passwords.
public sealed class PasswordResetAuditTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public PasswordResetAuditTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    private async Task<(IdentityDbContext, UserManager<ApplicationUser>, RoleManager<IdentityRole<Guid>>)> CreateContextAndManagersAsync(string databaseName)
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

        var roleStore = new IdentityRoleStore(context);
        var roleManager = new RoleManager<IdentityRole<Guid>>(
            roleStore,
            Enumerable.Empty<IRoleValidator<IdentityRole<Guid>>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!
        );

        return (context, userManager, roleManager);
    }

    private class IdentityRoleStore : IRoleStore<IdentityRole<Guid>>
    {
        private readonly IdentityDbContext _context;

        public IdentityRoleStore(IdentityDbContext context)
        {
            _context = context;
        }

        public async Task<IdentityResult> CreateAsync(IdentityRole<Guid> role, CancellationToken cancellationToken)
        {
            _context.Roles.Add(role);
            await _context.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> UpdateAsync(IdentityRole<Guid> role, CancellationToken cancellationToken)
        {
            _context.Roles.Update(role);
            await _context.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteAsync(IdentityRole<Guid> role, CancellationToken cancellationToken)
        {
            _context.Roles.Remove(role);
            await _context.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }

        public async Task<IdentityRole<Guid>?> FindByIdAsync(string id, CancellationToken cancellationToken)
        {
            return await _context.Roles.FindAsync(new object[] { Guid.Parse(id) }, cancellationToken: cancellationToken);
        }

        public async Task<IdentityRole<Guid>?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            return await _context.Roles.FirstOrDefaultAsync(r => r.NormalizedName == normalizedRoleName, cancellationToken);
        }

        public Task<string> GetRoleIdAsync(IdentityRole<Guid> role, CancellationToken cancellationToken) => Task.FromResult(role.Id.ToString());
        public Task<string?> GetRoleNameAsync(IdentityRole<Guid> role, CancellationToken cancellationToken) => Task.FromResult<string?>(role.Name);
        public Task SetRoleNameAsync(IdentityRole<Guid> role, string? roleName, CancellationToken cancellationToken)
        {
            role.Name = roleName;
            return Task.CompletedTask;
        }
        public Task<string?> GetNormalizedRoleNameAsync(IdentityRole<Guid> role, CancellationToken cancellationToken) => Task.FromResult<string?>(role.NormalizedName);
        public Task SetNormalizedRoleNameAsync(IdentityRole<Guid> role, string? normalizedName, CancellationToken cancellationToken)
        {
            role.NormalizedName = normalizedName;
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    [Fact]
    public async Task RecordPasswordResetInitiated_CreatesOutboxMessage_WithCorrectAction()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
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

            // Act: Record password reset initiated
            await userMgmtData.RecordPasswordResetInitiatedAsync(user, requestContext);

            // Assert: Outbox message with correct action
            var outboxMessages = db.OutboxMessages.ToList();
            Assert.Single(outboxMessages);

            var message = outboxMessages[0];
            Assert.Equal(typeof(EntityMutationAudited).FullName, message.ContractType);

            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                message.Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            );

            var audit = envelope.Payload;
            Assert.Equal(AuditActions.PasswordResetInitiated, audit.Action);
            Assert.Equal(user.Id, audit.EntityId);
        }
    }

    [Fact]
    public async Task RecordPasswordReset_CreatesOutboxMessage_WithCorrectAction()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
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

            // Act: Record password reset completed
            await userMgmtData.RecordPasswordResetAsync(user, requestContext);

            // Assert: Outbox message with correct action
            var outboxMessages = db.OutboxMessages.ToList();
            Assert.Single(outboxMessages);

            var message = outboxMessages[0];
            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                message.Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            );

            var audit = envelope.Payload;
            Assert.Equal(AuditActions.PasswordReset, audit.Action);
            Assert.Equal(user.Id, audit.EntityId);
            Assert.Contains("PasswordHash", audit.ChangedFields);
        }
    }

    [Fact]
    public async Task PasswordResetEvents_ContainCorrectMetadata()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "test@example.com",
                Email = "test@example.com",
            };
            await userManager.CreateAsync(user);

            var actorId = Guid.NewGuid().ToString();
            var userMgmtData = new UserManagementData(db);
            var requestContext = RequestContext.CreateNew(
                ActorContext.ForUser(actorId)
            );

            // Act: Record both reset events
            await userMgmtData.RecordPasswordResetInitiatedAsync(user, requestContext);

            db.OutboxMessages.RemoveRange(db.OutboxMessages);
            await db.SaveChangesAsync();

            await userMgmtData.RecordPasswordResetAsync(user, requestContext);

            // Assert: Metadata preserved in reset completion event
            var outboxMessage = db.OutboxMessages.First();
            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                outboxMessage.Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            );

            var audit = envelope.Payload;
            Assert.Equal(AuditSourceServices.Identity, audit.SourceService);
            Assert.Equal(AuditEntityTypes.ApplicationUser, audit.EntityType);
            Assert.Equal(actorId, audit.ActorId);
            Assert.NotEmpty(audit.TraceId);
            Assert.NotEmpty(audit.CorrelationId);
        }
    }

    [Fact]
    public async Task PasswordResetInitiationEvent_DoesNotIncludeToken()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
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

            // Act: Record password reset initiated
            await userMgmtData.RecordPasswordResetInitiatedAsync(user, requestContext);

            // Assert: No token in payload
            var outboxMessage = db.OutboxMessages.First();
            var payloadJson = outboxMessage.Payload;

            var forbiddenPatterns = new[] { "token", "pwd", "password", "hash", "secret", "apikey" };
            foreach (var pattern in forbiddenPatterns)
            {
                Assert.DoesNotContain(pattern, payloadJson, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task PasswordResetCompletionEvent_DoesNotIncludePasswordOrToken()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
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

            // Act: Record password reset completed
            await userMgmtData.RecordPasswordResetAsync(user, requestContext);

            // Assert: No sensitive data in payload
            var outboxMessage = db.OutboxMessages.First();
            var payloadJson = outboxMessage.Payload;

            var forbiddenPatterns = new[] { "password", "pwd", "hash", "secret", "token", "apikey", "privatekey" };
            foreach (var pattern in forbiddenPatterns)
            {
                Assert.DoesNotContain(pattern, payloadJson, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task PasswordResetEvent_OnlyRecordsFactNotCredential()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
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

            // Act: Record password reset completed
            await userMgmtData.RecordPasswordResetAsync(user, requestContext);

            // Assert: Event only records the fact, not the credential
            var outboxMessage = db.OutboxMessages.First();
            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                outboxMessage.Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            );

            var audit = envelope.Payload;
            Assert.NotNull(audit.NewValues);
            Assert.Single(audit.NewValues);
            var kvp = audit.NewValues.First();
            Assert.Equal("PasswordReset", kvp.Key);
            Assert.Equal("true", kvp.Value);
        }
    }
}
