using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Models.ServiceModels;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Business;

// IClientBusiness implementation for Client creation (CLIENT-001..004, AUDIT-001..003; backend.md,
// onion-boundaries.md). Owns exactly: normalizing business values, deciding the initial lifecycle
// status, deciding CLIENT-004 duplicate warnings, translating the command into the Client aggregate
// and the one EntityMutationAudited fact for the mutation, and persisting both through IClientData.
// No EF, cache, HttpContext, or Service Bus dependency - those belong to Data, Facade, and the
// outbox relay respectively.
public sealed class ClientBusiness : IClientBusiness
{
    private readonly IClientData _clientData;

    public ClientBusiness(IClientData clientData)
    {
        _clientData = clientData ?? throw new ArgumentNullException(nameof(clientData));
    }

    public async Task<ClientCreationResult> CreateAsync(CreateClientCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var normalizedName = NormalizeRequired(command.Name, nameof(command.Name));
        var normalizedOwnerUserId = NormalizeRequired(command.OwnerUserId, nameof(command.OwnerUserId));
        var normalizedEmail = NormalizeEmail(command.PrimaryEmail);
        var normalizedPhone = NormalizeOptional(command.PrimaryPhone);

        var lifecycleStatus = command.LifecycleStatus ?? ClientLifecycleStatus.Lead;
        if (!Enum.IsDefined(lifecycleStatus))
        {
            throw new ArgumentException(
                "Lifecycle status must be a defined ClientLifecycleStatus value.", nameof(command));
        }

        // Looked up before the new Client is built, so the new record can never match itself
        // (CLIENT-004).
        var duplicateCandidates = await _clientData.FindDuplicateCandidatesAsync(
            normalizedName, normalizedEmail, normalizedPhone, cancellationToken).ConfigureAwait(false);
        var possibleDuplicates = BuildDuplicateCandidates(duplicateCandidates, normalizedName, normalizedEmail, normalizedPhone);

        // CLIENT-001: only an identified actor (User or Service - the only ActorContext factories
        // that guarantee a non-null ActorId) can be attributed as CreatedBy.
        var createdBy = ResolveCreatedBy(command.Actor);

        var client = Client.Create(
            id: Guid.NewGuid(),
            name: normalizedName,
            lifecycleStatus: lifecycleStatus,
            ownerUserId: normalizedOwnerUserId,
            createdBy: createdBy,
            createdAtUtc: command.CreatedAtUtc,
            primaryContactName: NormalizeOptional(command.PrimaryContactName),
            primaryEmail: normalizedEmail,
            primaryPhone: normalizedPhone,
            website: NormalizeOptional(command.Website),
            addressLine: NormalizeOptional(command.AddressLine),
            city: NormalizeOptional(command.City),
            stateOrProvince: NormalizeOptional(command.StateOrProvince),
            postalCode: NormalizeOptional(command.PostalCode),
            country: NormalizeOptional(command.Country),
            description: NormalizeOptional(command.Description));

        var auditFact = BuildAuditFact(client, command);

        await _clientData.CreateAsync(client, auditFact, cancellationToken).ConfigureAwait(false);

        return new ClientCreationResult
        {
            Client = client,
            PossibleDuplicates = possibleDuplicates,
        };
    }

    private static EntityMutationAudited BuildAuditFact(Client client, CreateClientCommand command)
    {
        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(client.CreatedAtUtc, TimeSpan.Zero),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Client,
            EntityId = client.Id,
            Action = AuditActions.Created,
            ActorId = command.Actor.ActorId,
            ActorType = ResolveAuditActorType(command.Actor.ActorType),
            TraceId = command.RequestContext.TraceId,
            CorrelationId = command.RequestContext.CorrelationId,
            CausationId = command.RequestContext.CausationId,
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
            var matchedOn = new List<ClientDuplicateMatchField>();

            if (string.Equals(candidate.Name, normalizedName, StringComparison.Ordinal))
            {
                matchedOn.Add(ClientDuplicateMatchField.Name);
            }

            if (normalizedEmail is not null && string.Equals(candidate.PrimaryEmail, normalizedEmail, StringComparison.Ordinal))
            {
                matchedOn.Add(ClientDuplicateMatchField.PrimaryEmail);
            }

            if (normalizedPhone is not null && string.Equals(candidate.PrimaryPhone, normalizedPhone, StringComparison.Ordinal))
            {
                matchedOn.Add(ClientDuplicateMatchField.PrimaryPhone);
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
