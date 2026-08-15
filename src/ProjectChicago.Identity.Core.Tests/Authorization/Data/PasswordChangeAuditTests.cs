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

// Password change audit event generation tests (SEC-004, SEC-005, AUDIT-001..008, OUTBOX-001..006).
// Verify that password changes are recorded as audit events correctly without exposing password values.
public sealed class PasswordChangeAuditTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public PasswordChangeAuditTests(MsSqlContainerFixture fixture)
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
    public async Task RecordPasswordChanged_CreatesOutboxMessage_WithPasswordChangedAuditEvent()
    {
        var (db, userManager, roleManager) = await CreateContextAndManagersAsync($"IdentityDb_{Guid.NewGuid():N}");
        using (db)
        using (userManager)
        using (roleManager)
        {
            // Create user
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

            // Act: Record password changed
            await userMgmtData.RecordPasswordChangedAsync(user, requestContext);

            // Assert: Outbox message with correct password changed event
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
            Assert.Equal(AuditActions.PasswordChanged, audit.Action);
            Assert.Equal(user.Id, audit.EntityId);
            Assert.Contains("PasswordHash", audit.ChangedFields);
            Assert.NotNull(audit.NewValues);
            Assert.Contains(audit.NewValues, kvp => kvp.Key == "PasswordChanged" && kvp.Value == "true");
        }
    }

    [Fact]
    public async Task PasswordChange_AuditEvents_ContainCorrectMetadata()
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

            // Act: Record password changed
            await userMgmtData.RecordPasswordChangedAsync(user, requestContext);

            // Assert: Audit metadata preserved
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
    public async Task PasswordChangeAuditEvent_DoesNotIncludePasswordValue()
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

            // Act: Record password changed
            await userMgmtData.RecordPasswordChangedAsync(user, requestContext);

            // Assert: No sensitive data in payload
            var outboxMessage = db.OutboxMessages.First();
            var payloadJson = outboxMessage.Payload;

            var forbiddenPatterns = new[] { "password", "pwd", "hash", "secret", "apikey", "privatekey", "token" };
            foreach (var pattern in forbiddenPatterns)
            {
                Assert.DoesNotContain(pattern, payloadJson, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task PasswordChangeAuditEvent_OnlyRecordsFactNotCredential()
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

            // Act: Record password changed
            await userMgmtData.RecordPasswordChangedAsync(user, requestContext);

            // Assert: Event only records the fact, not the credential
            var outboxMessage = db.OutboxMessages.First();
            var envelope = EventEnvelopeSerializer.Deserialize<EntityMutationAudited>(
                outboxMessage.Payload,
                new[] { EntityMutationAudited.CurrentVersion }
            );

            var audit = envelope.Payload;

            // NewValues should only contain the fact that password changed, not the password itself
            Assert.NotNull(audit.NewValues);
            Assert.Single(audit.NewValues);
            var kvp = audit.NewValues.First();
            Assert.Equal("PasswordChanged", kvp.Key);
            Assert.Equal("true", kvp.Value);
        }
    }
}
