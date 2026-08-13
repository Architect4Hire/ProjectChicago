using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Models.ServiceModels;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.Shared.Correlation;
using CoreDuplicateMatchField = ProjectChicago.Crm.Core.Models.ServiceModels.ClientDuplicateMatchField;

namespace ProjectChicago.Crm.Core.Business;

// IClientBusiness implementation for Client creation (CLIENT-001..004, AUDIT-001..003; backend.md,
// onion-boundaries.md). Owns exactly: normalizing business values, deciding the initial lifecycle
// status, deciding CLIENT-004 duplicate warnings, translating the wire CreateClientViewModel into
// the Client aggregate and the one EntityMutationAudited fact for the mutation, persisting both
// through IClientData, and mapping the result into the wire ClientServiceModel
// (ClientContractMappingExtensions). No EF, cache, HttpContext, or Service Bus dependency - those
// belong to Data, Facade, and the outbox relay respectively.
public sealed class ClientBusiness : IClientBusiness
{
    private readonly IClientData _clientData;

    public ClientBusiness(IClientData clientData)
    {
        _clientData = clientData ?? throw new ArgumentNullException(nameof(clientData));
    }

    public async Task<ClientServiceModel> CreateAsync(
        CreateClientViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedName = NormalizeRequired(request.Name, nameof(request.Name));
        var normalizedOwnerUserId = NormalizeRequired(request.OwnerUserId, nameof(request.OwnerUserId));
        var normalizedEmail = NormalizeEmail(request.PrimaryEmail);
        var normalizedPhone = NormalizeOptional(request.PrimaryPhone);

        // ToCoreLifecycleStatus throws ArgumentOutOfRangeException for an undefined wire value
        // before the CLIENT-010 Lead default is ever considered, so an out-of-range value is
        // rejected the same way whether or not it was caught by CreateClientViewModel's own
        // [EnumDataType] validation upstream.
        var lifecycleStatus = request.LifecycleStatus is { } status
            ? status.ToCoreLifecycleStatus()
            : ClientLifecycleStatus.Lead;

        // Looked up before the new Client is built, so the new record can never match itself
        // (CLIENT-004).
        var duplicateCandidates = await _clientData.FindDuplicateCandidatesAsync(
            normalizedName, normalizedEmail, normalizedPhone, cancellationToken).ConfigureAwait(false);
        var possibleDuplicates = BuildDuplicateCandidates(duplicateCandidates, normalizedName, normalizedEmail, normalizedPhone);

        // CLIENT-001: only an identified actor (User or Service - the only ActorContext factories
        // that guarantee a non-null ActorId) can be attributed as CreatedBy.
        var createdBy = ResolveCreatedBy(actor);

        var client = Client.Create(
            id: Guid.NewGuid(),
            name: normalizedName,
            lifecycleStatus: lifecycleStatus,
            ownerUserId: normalizedOwnerUserId,
            createdBy: createdBy,
            createdAtUtc: createdAtUtc,
            primaryContactName: NormalizeOptional(request.PrimaryContactName),
            primaryEmail: normalizedEmail,
            primaryPhone: normalizedPhone,
            website: NormalizeOptional(request.Website),
            addressLine: NormalizeOptional(request.AddressLine),
            city: NormalizeOptional(request.City),
            stateOrProvince: NormalizeOptional(request.StateOrProvince),
            postalCode: NormalizeOptional(request.PostalCode),
            country: NormalizeOptional(request.Country),
            description: NormalizeOptional(request.Description));

        var auditFact = BuildAuditFact(client, actor, requestContext);

        await _clientData.CreateAsync(client, auditFact, cancellationToken).ConfigureAwait(false);

        return client.ToServiceModel(possibleDuplicates);
    }

    public async Task<ClientServiceModel?> ChangeLifecycleStatusAsync(
        Guid clientId,
        ClientLifecycleStatusContract newStatus,
        string expectedConcurrencyToken,
        ActorContext actor,
        RequestContext requestContext,
        DateTime changedAtUtc,
        CancellationToken cancellationToken)
    {
        var coreNewStatus = newStatus.ToCoreLifecycleStatus();

        // GetForLifecycleChangeAsync throws ClientConcurrencyConflictException itself when
        // expectedConcurrencyToken is stale (DATA-008), so a caller acting on an old read never
        // reaches the CLIENT-010..015 transition-rule check below with data it never actually saw.
        var client = await _clientData.GetForLifecycleChangeAsync(
            clientId, expectedConcurrencyToken, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return null;
        }

        var previousStatus = client.LifecycleStatus;
        if (!ClientLifecycleTransitionRules.IsAllowed(previousStatus, coreNewStatus))
        {
            throw new InvalidOperationException(
                $"Client lifecycle status cannot transition from '{previousStatus}' to '{coreNewStatus}'.");
        }

        // CLIENT-001-equivalent for a mutation: only an identified actor can be attributed as the
        // modifier of a StatusChanged audit fact.
        var modifiedBy = ResolveModifiedBy(actor);
        client.ChangeLifecycleStatus(coreNewStatus, modifiedBy, changedAtUtc);

        var auditFact = BuildLifecycleAuditFact(client, previousStatus, actor, requestContext);

        await _clientData.SaveLifecycleChangeAsync(client, auditFact, cancellationToken).ConfigureAwait(false);

        return client.ToServiceModel([]);
    }

    public async Task<PagedResponse<ClientServiceModel>> ListAsync(
        ListClientsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = new ClientListFilter
        {
            Search = NormalizeOptional(request.Search),
            LifecycleStatus = request.LifecycleStatus?.ToCoreLifecycleStatus(),
            OwnerUserId = NormalizeOptional(request.OwnerUserId),
            IsActive = request.IsActive,
            // CLIENT-023 default sort: Name ascending - the same fallback ClientRepository.ApplySort
            // applies for an unmatched ClientListSortField, so "no sort requested" and "an unmapped
            // sort field" never disagree about the default ordering.
            SortBy = request.SortBy?.ToCoreListSortField() ?? ClientListSortField.Name,
            SortDirection = request.SortDirection?.ToCoreListSortDirection() ?? ClientListSortDirection.Ascending,
            Page = request.Page,
            PageSize = request.PageSize,
        };

        var result = await _clientData.ListAsync(filter, cancellationToken).ConfigureAwait(false);

        return new PagedResponse<ClientServiceModel>
        {
            // possibleDuplicates is always empty for a list result - CLIENT-004 duplicate detection
            // is a creation-time concern, not something a list/search result recomputes per row.
            Items = result.Items.Select(client => client.ToServiceModel([])).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = request.PageSize > 0
                ? (int)Math.Ceiling(result.TotalCount / (double)request.PageSize)
                : 0,
        };
    }

    public async Task<ClientDetailServiceModel?> GetDetailAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var detail = await _clientData.GetDetailAsync(clientId, cancellationToken).ConfigureAwait(false);

        return detail?.ToClientDetailServiceModel();
    }

    private static EntityMutationAudited BuildAuditFact(Client client, ActorContext actor, RequestContext requestContext)
    {
        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(client.CreatedAtUtc, TimeSpan.Zero),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Client,
            EntityId = client.Id,
            Action = AuditActions.Created,
            ActorId = actor.ActorId,
            ActorType = ResolveAuditActorType(actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            // Field names only, never values (AUDIT-008) - a Created fact has no "previous" state to
            // disclose, and this microstep does not decide which "new" values are safe to publish.
            ChangedFields = BuildChangedFields(client),
        };
    }

    // Lists the business fields this creation actually populated. Filtered through
    // AuditSensitiveFieldNames defensively, even though none of Client's own field names are
    // sensitive today - the same guard every publisher is expected to apply (AUDIT-008).
    private static IReadOnlyList<string> BuildChangedFields(Client client)
    {
        var fields = new List<string>
        {
            nameof(Client.Name),
            nameof(Client.LifecycleStatus),
            nameof(Client.OwnerUserId),
        };

        AddIfPresent(fields, nameof(Client.PrimaryContactName), client.PrimaryContactName);
        AddIfPresent(fields, nameof(Client.PrimaryEmail), client.PrimaryEmail);
        AddIfPresent(fields, nameof(Client.PrimaryPhone), client.PrimaryPhone);
        AddIfPresent(fields, nameof(Client.Website), client.Website);
        AddIfPresent(fields, nameof(Client.AddressLine), client.AddressLine);
        AddIfPresent(fields, nameof(Client.City), client.City);
        AddIfPresent(fields, nameof(Client.StateOrProvince), client.StateOrProvince);
        AddIfPresent(fields, nameof(Client.PostalCode), client.PostalCode);
        AddIfPresent(fields, nameof(Client.Country), client.Country);
        AddIfPresent(fields, nameof(Client.Description), client.Description);

        return fields.Where(field => !AuditSensitiveFieldNames.IsForbidden(field)).ToList();
    }

    private static void AddIfPresent(List<string> fields, string fieldName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(fieldName);
        }
    }

    // AUDIT-002's Previous/New values for the one field this use case changes. LifecycleStatus's
    // enum-member-name form is never sensitive (AuditSensitiveFieldNames), so both values are safe
    // to disclose (AUDIT-008) - unlike BuildAuditFact's Created-fact ChangedFields, this fact also
    // carries the actual before/after values because CLIENT-011/012 requires the lifecycle history
    // itself to be reconstructable from audit, not just the fact that "something" changed.
    private static EntityMutationAudited BuildLifecycleAuditFact(
        Client client, ClientLifecycleStatus previousStatus, ActorContext actor, RequestContext requestContext)
    {
        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(client.LastModifiedAtUtc, TimeSpan.Zero),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Client,
            EntityId = client.Id,
            Action = AuditActions.StatusChanged,
            ActorId = actor.ActorId,
            ActorType = ResolveAuditActorType(actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = [nameof(Client.LifecycleStatus)],
            PreviousValues = new Dictionary<string, string>
            {
                [nameof(Client.LifecycleStatus)] = previousStatus.ToString(),
            },
            NewValues = new Dictionary<string, string>
            {
                [nameof(Client.LifecycleStatus)] = client.LifecycleStatus.ToString(),
            },
        };
    }

    private static string ResolveModifiedBy(ActorContext actor) =>
        string.IsNullOrWhiteSpace(actor.ActorId)
            ? throw new ArgumentException(
                "Client lifecycle transitions require an identified actor (User or Service) with a resolved ActorId.",
                nameof(actor))
            : actor.ActorId;

    private static IReadOnlyList<ClientDuplicateCandidate> BuildDuplicateCandidates(
        IReadOnlyList<Client> candidates,
        string normalizedName,
        string? normalizedEmail,
        string? normalizedPhone)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var results = new List<ClientDuplicateCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var matchedOn = new List<CoreDuplicateMatchField>();

            if (string.Equals(candidate.Name, normalizedName, StringComparison.Ordinal))
            {
                matchedOn.Add(CoreDuplicateMatchField.Name);
            }

            if (normalizedEmail is not null && string.Equals(candidate.PrimaryEmail, normalizedEmail, StringComparison.Ordinal))
            {
                matchedOn.Add(CoreDuplicateMatchField.PrimaryEmail);
            }

            if (normalizedPhone is not null && string.Equals(candidate.PrimaryPhone, normalizedPhone, StringComparison.Ordinal))
            {
                matchedOn.Add(CoreDuplicateMatchField.PrimaryPhone);
            }

            results.Add(new ClientDuplicateCandidate
            {
                ClientId = candidate.Id,
                Name = candidate.Name,
                MatchedOn = matchedOn,
            });
        }

        return results;
    }

    private static string ResolveCreatedBy(ActorContext actor) =>
        string.IsNullOrWhiteSpace(actor.ActorId)
            ? throw new ArgumentException(
                "Client creation requires an identified actor (User or Service) with a resolved ActorId.",
                nameof(actor))
            : actor.ActorId;

    private static string ResolveAuditActorType(ActorType actorType) => actorType switch
    {
        ActorType.User => AuditActorTypes.User,
        ActorType.Service => AuditActorTypes.Service,
        ActorType.System => AuditActorTypes.System,
        ActorType.Anonymous => AuditActorTypes.Anonymous,
        _ => throw new ArgumentException(
            $"Actor type '{actorType}' cannot be resolved to a known audit actor type.", nameof(actorType)),
    };

    private static string NormalizeRequired(string value, string paramName)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? throw new ArgumentException("Value cannot be null or whitespace.", paramName)
            : trimmed;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // CLIENT-004 duplicate matching and CLIENT-002 storage both need a stable comparison form;
    // email is normalized to lowercase so "Jane@Acme.example" and "jane@acme.example" are treated
    // as the same address.
    private static string? NormalizeEmail(string? value) => NormalizeOptional(value)?.ToLowerInvariant();
}
