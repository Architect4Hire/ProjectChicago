using ProjectChicago.Contracts.Audit;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;
using ProjectChicago.Identity.Core.Persistence;
using ProjectChicago.Shared.Correlation;
using ProjectChicago.Shared.Messaging;
using ProjectChicago.Shared.Outbox;

namespace ProjectChicago.Identity.Core.Authorization.Data;

// User management audit event persistence (SEC-004, SEC-010..016, AUDIT-001..008, OUTBOX-001..006).
// Writes EntityMutationAudited events for user creation to the transactional outbox.
// Never captures passwords, password hashes, or credential material; only audit action, actor ID,
// occurred-at UTC, and W3C trace/correlation context.
public sealed class UserManagementData
{
    private readonly IdentityDbContext _dbContext;

    public UserManagementData(IdentityDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    // Record user creation audit event (SEC-004, AUDIT-001).
    public async Task RecordUserCreatedAsync(
        ApplicationUser user,
        string roleName,
        RequestContext requestContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        var newValues = new Dictionary<string, string>
        {
            { "Email", user.Email ?? "" },
            { "RoleName", roleName },
        };

        var auditEvent = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString("N"),
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            SourceService = AuditSourceServices.Identity,
            EntityType = AuditEntityTypes.ApplicationUser,
            EntityId = user.Id,
            Action = AuditActions.UserCreated,
            ActorId = requestContext.Actor.ActorId,
            ActorType = MapActorType(requestContext.Actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = new[] { "Email", "RoleName" },
            PreviousValues = null,
            NewValues = newValues,
        };

        await AddAuditEventToOutboxAsync(auditEvent, requestContext, cancellationToken).ConfigureAwait(false);
    }

    // Record user deactivation audit event (SEC-004, AUDIT-001).
    public async Task RecordUserDeactivatedAsync(
        ApplicationUser user,
        RequestContext requestContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var auditEvent = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString("N"),
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            SourceService = AuditSourceServices.Identity,
            EntityType = AuditEntityTypes.ApplicationUser,
            EntityId = user.Id,
            Action = AuditActions.UserDeactivated,
            ActorId = requestContext.Actor.ActorId,
            ActorType = MapActorType(requestContext.Actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = new[] { "LockoutEnd" },
            PreviousValues = null,
            NewValues = new Dictionary<string, string> { { "LockoutEnd", DateTimeOffset.MaxValue.ToString("O") } },
        };

        await AddAuditEventToOutboxAsync(auditEvent, requestContext, cancellationToken).ConfigureAwait(false);
    }

    // Record user activation audit event (SEC-004, AUDIT-001).
    public async Task RecordUserActivatedAsync(
        ApplicationUser user,
        RequestContext requestContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var auditEvent = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString("N"),
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            SourceService = AuditSourceServices.Identity,
            EntityType = AuditEntityTypes.ApplicationUser,
            EntityId = user.Id,
            Action = AuditActions.UserActivated,
            ActorId = requestContext.Actor.ActorId,
            ActorType = MapActorType(requestContext.Actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = new[] { "LockoutEnd" },
            PreviousValues = null,
            NewValues = new Dictionary<string, string> { { "LockoutEnd", "null" } },
        };

        await AddAuditEventToOutboxAsync(auditEvent, requestContext, cancellationToken).ConfigureAwait(false);
    }

    // Record role added to user audit event (SEC-004, AUDIT-001).
    public async Task RecordRoleAddedAsync(
        ApplicationUser user,
        string roleName,
        RequestContext requestContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        var auditEvent = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString("N"),
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            SourceService = AuditSourceServices.Identity,
            EntityType = AuditEntityTypes.ApplicationUser,
            EntityId = user.Id,
            Action = AuditActions.RoleAdded,
            ActorId = requestContext.Actor.ActorId,
            ActorType = MapActorType(requestContext.Actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = new[] { "Roles" },
            PreviousValues = null,
            NewValues = new Dictionary<string, string> { { "RoleAdded", roleName } },
        };

        await AddAuditEventToOutboxAsync(auditEvent, requestContext, cancellationToken).ConfigureAwait(false);
    }

    // Record role removed from user audit event (SEC-004, AUDIT-001).
    public async Task RecordRoleRemovedAsync(
        ApplicationUser user,
        string roleName,
        RequestContext requestContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        var auditEvent = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString("N"),
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            SourceService = AuditSourceServices.Identity,
            EntityType = AuditEntityTypes.ApplicationUser,
            EntityId = user.Id,
            Action = AuditActions.RoleRemoved,
            ActorId = requestContext.Actor.ActorId,
            ActorType = MapActorType(requestContext.Actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = new[] { "Roles" },
            PreviousValues = null,
            NewValues = new Dictionary<string, string> { { "RoleRemoved", roleName } },
        };

        await AddAuditEventToOutboxAsync(auditEvent, requestContext, cancellationToken).ConfigureAwait(false);
    }

    // Record password change audit event (SEC-004, SEC-005, AUDIT-001).
    // Records the fact that a password was changed, never the password value itself.
    public async Task RecordPasswordChangedAsync(
        ApplicationUser user,
        RequestContext requestContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var auditEvent = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString("N"),
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            SourceService = AuditSourceServices.Identity,
            EntityType = AuditEntityTypes.ApplicationUser,
            EntityId = user.Id,
            Action = AuditActions.PasswordChanged,
            ActorId = requestContext.Actor.ActorId,
            ActorType = MapActorType(requestContext.Actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = new[] { "PasswordHash" },
            PreviousValues = null,
            NewValues = new Dictionary<string, string> { { "PasswordChanged", "true" } },
        };

        await AddAuditEventToOutboxAsync(auditEvent, requestContext, cancellationToken).ConfigureAwait(false);
    }

    // Record password reset initiation audit event (admin-only, SEC-004, SEC-005, AUDIT-001).
    // Records that an admin initiated a password reset for a user; never records the token.
    public async Task RecordPasswordResetInitiatedAsync(
        ApplicationUser user,
        RequestContext requestContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var auditEvent = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString("N"),
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            SourceService = AuditSourceServices.Identity,
            EntityType = AuditEntityTypes.ApplicationUser,
            EntityId = user.Id,
            Action = AuditActions.PasswordResetInitiated,
            ActorId = requestContext.Actor.ActorId,
            ActorType = MapActorType(requestContext.Actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = new[] { "PasswordReset" },
            PreviousValues = null,
            NewValues = new Dictionary<string, string> { { "PasswordResetInitiated", "true" } },
        };

        await AddAuditEventToOutboxAsync(auditEvent, requestContext, cancellationToken).ConfigureAwait(false);
    }

    // Record password reset completion audit event (SEC-004, SEC-005, AUDIT-001).
    // Records that a password reset was completed; never records the token or new password.
    public async Task RecordPasswordResetAsync(
        ApplicationUser user,
        RequestContext requestContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var auditEvent = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString("N"),
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            SourceService = AuditSourceServices.Identity,
            EntityType = AuditEntityTypes.ApplicationUser,
            EntityId = user.Id,
            Action = AuditActions.PasswordReset,
            ActorId = requestContext.Actor.ActorId,
            ActorType = MapActorType(requestContext.Actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = new[] { "PasswordHash" },
            PreviousValues = null,
            NewValues = new Dictionary<string, string> { { "PasswordReset", "true" } },
        };

        await AddAuditEventToOutboxAsync(auditEvent, requestContext, cancellationToken).ConfigureAwait(false);
    }

    // Persist audit event to outbox atomically (OUTBOX-003..006).
    private async Task AddAuditEventToOutboxAsync(
        EntityMutationAudited auditEvent,
        RequestContext requestContext,
        CancellationToken cancellationToken)
    {
        var envelope = new EventEnvelope<EntityMutationAudited>
        {
            EventId = auditEvent.EventId,
            ContractType = typeof(EntityMutationAudited).FullName!,
            ContractVersion = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = auditEvent.OccurredAtUtc,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            TraceId = requestContext.TraceId,
            Payload = auditEvent,
        };

        var serialized = EventEnvelopeSerializer.Serialize(envelope);

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            ContractType = envelope.ContractType,
            ContractVersion = envelope.ContractVersion,
            Payload = serialized,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            TraceId = envelope.TraceId,
            OccurredAtUtc = auditEvent.OccurredAtUtc.UtcDateTime,
            CreatedAtUtc = DateTime.UtcNow,
        };

        _dbContext.OutboxMessages.Add(outboxMessage);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // Map ActorType enum to audit contract string value.
    private static string MapActorType(ActorType actorType) => actorType switch
    {
        ActorType.User => AuditActorTypes.User,
        ActorType.Service => AuditActorTypes.Service,
        ActorType.System => AuditActorTypes.System,
        ActorType.Anonymous => AuditActorTypes.Anonymous,
        _ => AuditActorTypes.Anonymous,
    };
}
