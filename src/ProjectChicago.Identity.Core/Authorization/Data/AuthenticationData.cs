using ProjectChicago.Contracts.Audit;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;
using ProjectChicago.Identity.Core.Persistence;
using ProjectChicago.Shared.Correlation;
using ProjectChicago.Shared.Messaging;
using ProjectChicago.Shared.Outbox;

namespace ProjectChicago.Identity.Core.Authorization.Data;

// Authentication audit event persistence (SEC-001..025, AUDIT-001..008, OUTBOX-001..006).
// Writes EntityMutationAudited events to the transactional outbox atomically with auth mutations.
// Never captures passwords, tokens, or credential material; only audit action, actor ID, occurred-at UTC,
// and W3C trace/correlation context. Failed login attempts also generate audit events (they do not mutate
// auth state but must remain auditable for investigation and compliance).
public sealed class AuthenticationData
{
    private readonly IdentityDbContext _dbContext;

    public AuthenticationData(IdentityDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    // Record successful login audit event (SEC-005, AUDIT-001).
    public async Task RecordLoginSuccessAsync(
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
            EntityType = AuditEntityTypes.AuthenticationSession,
            EntityId = user.Id,
            Action = AuditActions.LoggedIn,
            ActorId = user.Id.ToString(),
            ActorType = AuditActorTypes.User,
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = [],
            PreviousValues = null,
            NewValues = null,
        };

        await AddAuditEventToOutboxAsync(auditEvent, requestContext, cancellationToken).ConfigureAwait(false);
    }

    // Record failed login audit event (SEC-020..025, AUDIT-001).
    // Failed login does not mutate ApplicationUser or auth state but remains auditable for compliance.
    public async Task RecordFailedLoginAsync(
        string attemptedUsername,
        RequestContext requestContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptedUsername);

        var auditEvent = new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString("N"),
            Version = EntityMutationAudited.CurrentVersion,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            SourceService = AuditSourceServices.Identity,
            EntityType = AuditEntityTypes.AuthenticationSession,
            EntityId = Guid.Empty, // No user entity created by failed login
            Action = AuditActions.FailedLogin,
            ActorId = null, // Actor is unknown on failed login; username alone is not sufficient actor ID
            ActorType = AuditActorTypes.Anonymous,
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = [],
            PreviousValues = null,
            NewValues = null,
        };

        await AddAuditEventToOutboxAsync(auditEvent, requestContext, cancellationToken).ConfigureAwait(false);
    }

    // Record account-locked audit event (SEC-020, AUDIT-001).
    public async Task RecordAccountLockedAsync(
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
            EntityType = AuditEntityTypes.AuthenticationSession,
            EntityId = user.Id,
            Action = AuditActions.AccountLocked,
            ActorId = null, // Lockout is automatic/system-triggered, not a user action
            ActorType = AuditActorTypes.System,
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = [],
            PreviousValues = null,
            NewValues = null,
        };

        await AddAuditEventToOutboxAsync(auditEvent, requestContext, cancellationToken).ConfigureAwait(false);
    }

    // Record successful logout audit event (SEC-005, AUDIT-001).
    public async Task RecordLogoutAsync(
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
            EntityType = AuditEntityTypes.AuthenticationSession,
            EntityId = user.Id,
            Action = AuditActions.LoggedOut,
            ActorId = user.Id.ToString(),
            ActorType = AuditActorTypes.User,
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = [],
            PreviousValues = null,
            NewValues = null,
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
}
