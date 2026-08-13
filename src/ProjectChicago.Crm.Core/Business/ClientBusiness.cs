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

    public async Task<ClientServiceModel?> ArchiveAsync(
        Guid clientId,
        string expectedConcurrencyToken,
        ActorContext actor,
        RequestContext requestContext,
        DateTime archivedAtUtc,
        CancellationToken cancellationToken)
    {
        // GetForArchiveAsync throws ClientConcurrencyConflictException itself when
        // expectedConcurrencyToken is stale (DATA-008), so a caller acting on an old read never
        // reaches the CLIENT-015 active-project check below with data it never actually saw.
        var client = await _clientData.GetForArchiveAsync(
            clientId, expectedConcurrencyToken, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return null;
        }

        // CLIENT-015: A Client containing active Projects shall not be permanently removed from the
        // system. Since Archive is non-destructive (CLIENT-014), we enforce that archiving is only
        // allowed when there are no active Projects.
        var hasActiveProjects = await _clientData.HasActiveProjectsAsync(clientId, cancellationToken)
            .ConfigureAwait(false);
        if (hasActiveProjects)
        {
            throw new InvalidOperationException(
                "A Client containing active Projects cannot be archived (CLIENT-015).");
        }

        var previousStatus = client.LifecycleStatus;
        client.ChangeLifecycleStatus(ClientLifecycleStatus.Archived, ResolveModifiedBy(actor), archivedAtUtc);

        var auditFact = BuildArchiveAuditFact(client, previousStatus, actor, requestContext);

        await _clientData.SaveArchiveAsync(client, auditFact, cancellationToken).ConfigureAwait(false);

        return client.ToServiceModel([]);
    }

    public async Task<ClientServiceModel?> RestoreAsync(
        Guid clientId,
        ClientLifecycleStatusContract restoredStatus,
        string expectedConcurrencyToken,
        ActorContext actor,
        RequestContext requestContext,
        DateTime restoredAtUtc,
        CancellationToken cancellationToken)
    {
        var coreRestoredStatus = restoredStatus.ToCoreLifecycleStatus();

        // GetForRestoreAsync throws ClientConcurrencyConflictException itself when
        // expectedConcurrencyToken is stale (DATA-008), so a caller acting on an old read never
        // reaches the Archived-status check below with data it never actually saw.
        var client = await _clientData.GetForRestoreAsync(
            clientId, expectedConcurrencyToken, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return null;
        }

        // Restore is only valid when transitioning from Archived status to a non-Archived status.
        // This is distinct from ChangeLifecycleStatusAsync which uses the general transition rules.
        if (client.LifecycleStatus != ClientLifecycleStatus.Archived)
        {
            throw new InvalidOperationException(
                $"Client restore can only be performed on Archived Clients. Current status: '{client.LifecycleStatus}'.");
        }

        if (coreRestoredStatus == ClientLifecycleStatus.Archived)
        {
            throw new InvalidOperationException(
                "Restore cannot transition a Client to Archived status. Use Archive for that operation.");
        }

        var previousStatus = client.LifecycleStatus;
        client.ChangeLifecycleStatus(coreRestoredStatus, ResolveModifiedBy(actor), restoredAtUtc);

        var auditFact = BuildRestoreAuditFact(client, previousStatus, actor, requestContext);

        await _clientData.SaveRestoreAsync(client, auditFact, cancellationToken).ConfigureAwait(false);

        return client.ToServiceModel([]);
    }

    public async Task<ClientServiceModel?> UpdateAsync(
        Guid clientId,
        UpdateClientViewModel request,
        string expectedConcurrencyToken,
        ActorContext actor,
        RequestContext requestContext,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // GetForUpdateAsync throws ClientConcurrencyConflictException itself when
        // expectedConcurrencyToken is stale (DATA-008), so a caller acting on an old read never
        // reaches the update below with data it never actually saw.
        var client = await _clientData.GetForUpdateAsync(
            clientId, expectedConcurrencyToken, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return null;
        }

        // Capture before values for audit (AUDIT-002)
        var beforeValues = CaptureBeforeValues(client);

        // Apply updates only to fields provided in the request (null means caller did not include field)
        var updatedName = request.Name is not null ? NormalizeRequired(request.Name, nameof(request.Name)) : client.Name;
        var updatedContactName = request.PrimaryContactName is not null ? NormalizeOptional(request.PrimaryContactName) : client.PrimaryContactName;
        var updatedEmail = request.PrimaryEmail is not null ? NormalizeEmail(request.PrimaryEmail) : client.PrimaryEmail;
        var updatedPhone = request.PrimaryPhone is not null ? NormalizeOptional(request.PrimaryPhone) : client.PrimaryPhone;
        var updatedWebsite = request.Website is not null ? NormalizeOptional(request.Website) : client.Website;
        var updatedAddress = request.AddressLine is not null ? NormalizeOptional(request.AddressLine) : client.AddressLine;
        var updatedCity = request.City is not null ? NormalizeOptional(request.City) : client.City;
        var updatedStateOrProvince = request.StateOrProvince is not null ? NormalizeOptional(request.StateOrProvince) : client.StateOrProvince;
        var updatedPostalCode = request.PostalCode is not null ? NormalizeOptional(request.PostalCode) : client.PostalCode;
        var updatedCountry = request.Country is not null ? NormalizeOptional(request.Country) : client.Country;
        var updatedDescription = request.Description is not null ? NormalizeOptional(request.Description) : client.Description;
        var updatedOwnerUserId = request.OwnerUserId is not null ? NormalizeRequired(request.OwnerUserId, nameof(request.OwnerUserId)) : client.OwnerUserId;

        var modifiedBy = ResolveModifiedBy(actor);
        client.UpdateProfile(
            name: updatedName,
            primaryContactName: updatedContactName,
            primaryEmail: updatedEmail,
            primaryPhone: updatedPhone,
            website: updatedWebsite,
            addressLine: updatedAddress,
            city: updatedCity,
            stateOrProvince: updatedStateOrProvince,
            postalCode: updatedPostalCode,
            country: updatedCountry,
            description: updatedDescription,
            ownerUserId: updatedOwnerUserId,
            modifiedBy: modifiedBy,
            modifiedAtUtc: modifiedAtUtc);

        // Capture after values and build changed fields list
        var afterValues = CaptureAfterValues(client);
        var changedFields = BuildChangedFieldsForUpdate(beforeValues, afterValues);

        var auditFact = BuildUpdateAuditFact(client, beforeValues, afterValues, changedFields, actor, requestContext);

        await _clientData.SaveUpdateAsync(client, auditFact, cancellationToken).ConfigureAwait(false);

        return client.ToServiceModel([]);
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

    // AUDIT-002's Previous/New values for archive operations. LifecycleStatus's enum-member-name
    // form is never sensitive (AuditSensitiveFieldNames), so both values are safe to disclose
    // (AUDIT-008). Archive transitions to the Archived status.
    private static EntityMutationAudited BuildArchiveAuditFact(
        Client client, ClientLifecycleStatus previousStatus, ActorContext actor, RequestContext requestContext)
    {
        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(client.LastModifiedAtUtc, TimeSpan.Zero),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Client,
            EntityId = client.Id,
            Action = AuditActions.Archived,
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

    // AUDIT-002's Previous/New values for restore operations. LifecycleStatus's enum-member-name
    // form is never sensitive (AuditSensitiveFieldNames), so both values are safe to disclose
    // (AUDIT-008). Restore transitions from Archived to a new status.
    private static EntityMutationAudited BuildRestoreAuditFact(
        Client client, ClientLifecycleStatus previousStatus, ActorContext actor, RequestContext requestContext)
    {
        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(client.LastModifiedAtUtc, TimeSpan.Zero),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Client,
            EntityId = client.Id,
            Action = AuditActions.Restored,
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

    // Captures the current state of all editable Client fields for comparison with post-update state
    // (AUDIT-002: before/after values).
    private static Dictionary<string, string?> CaptureBeforeValues(Client client)
    {
        return new Dictionary<string, string?>
        {
            [nameof(Client.Name)] = client.Name,
            [nameof(Client.PrimaryContactName)] = client.PrimaryContactName,
            [nameof(Client.PrimaryEmail)] = client.PrimaryEmail,
            [nameof(Client.PrimaryPhone)] = client.PrimaryPhone,
            [nameof(Client.Website)] = client.Website,
            [nameof(Client.AddressLine)] = client.AddressLine,
            [nameof(Client.City)] = client.City,
            [nameof(Client.StateOrProvince)] = client.StateOrProvince,
            [nameof(Client.PostalCode)] = client.PostalCode,
            [nameof(Client.Country)] = client.Country,
            [nameof(Client.Description)] = client.Description,
            [nameof(Client.OwnerUserId)] = client.OwnerUserId,
        };
    }

    // Captures the updated state of all editable Client fields for comparison with pre-update state
    // (AUDIT-002: before/after values).
    private static Dictionary<string, string?> CaptureAfterValues(Client client)
    {
        return new Dictionary<string, string?>
        {
            [nameof(Client.Name)] = client.Name,
            [nameof(Client.PrimaryContactName)] = client.PrimaryContactName,
            [nameof(Client.PrimaryEmail)] = client.PrimaryEmail,
            [nameof(Client.PrimaryPhone)] = client.PrimaryPhone,
            [nameof(Client.Website)] = client.Website,
            [nameof(Client.AddressLine)] = client.AddressLine,
            [nameof(Client.City)] = client.City,
            [nameof(Client.StateOrProvince)] = client.StateOrProvince,
            [nameof(Client.PostalCode)] = client.PostalCode,
            [nameof(Client.Country)] = client.Country,
            [nameof(Client.Description)] = client.Description,
            [nameof(Client.OwnerUserId)] = client.OwnerUserId,
        };
    }

    // Determines which fields changed between before and after (AUDIT-002: ChangedFields).
    // Field names only, never values (AUDIT-008).
    private static List<string> BuildChangedFieldsForUpdate(
        Dictionary<string, string?> beforeValues,
        Dictionary<string, string?> afterValues)
    {
        var changedFields = new List<string>();

        foreach (var key in beforeValues.Keys)
        {
            if (!string.Equals(beforeValues[key], afterValues[key], StringComparison.Ordinal))
            {
                changedFields.Add(key);
            }
        }

        // Filter through AuditSensitiveFieldNames defensively (AUDIT-008), even though none of
        // Client's field names are sensitive today - the same guard every publisher is expected to apply.
        return changedFields.Where(field => !AuditSensitiveFieldNames.IsForbidden(field)).ToList();
    }

    // AUDIT-002's Updated fact with before/after values for all changed fields. Field values are
    // safe to disclose here since Client fields are not sensitive (AuditSensitiveFieldNames).
    // Every changed field is represented in both PreviousValues and NewValues.
    private static EntityMutationAudited BuildUpdateAuditFact(
        Client client,
        Dictionary<string, string?> beforeValues,
        Dictionary<string, string?> afterValues,
        List<string> changedFields,
        ActorContext actor,
        RequestContext requestContext)
    {
        var previousValues = new Dictionary<string, string>();
        var newValues = new Dictionary<string, string>();

        foreach (var fieldName in changedFields)
        {
            var before = beforeValues[fieldName];
            var after = afterValues[fieldName];

            // Represent before/after as string; null values become null in the dict (which is safe
            // for JSON serialization via EntityMutationAudited's handling)
            if (before is not null)
            {
                previousValues[fieldName] = before;
            }

            if (after is not null)
            {
                newValues[fieldName] = after;
            }
        }

        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(client.LastModifiedAtUtc, TimeSpan.Zero),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Client,
            EntityId = client.Id,
            Action = AuditActions.Updated,
            ActorId = actor.ActorId,
            ActorType = ResolveAuditActorType(actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = changedFields,
            PreviousValues = previousValues.Count > 0 ? previousValues : null,
            NewValues = newValues.Count > 0 ? newValues : null,
        };
    }
}
