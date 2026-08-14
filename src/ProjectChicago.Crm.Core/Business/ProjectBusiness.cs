using ProjectChicago.Contracts.Audit;
using ProjectChicago.Crm.Contracts.Common;
using ProjectChicago.Crm.Contracts.Projects;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Crm.Core.Business;

// IProjectBusiness implementation for Project creation (PROJECT-001..002, AUDIT-001..003;
// backend.md, onion-boundaries.md). Owns exactly: normalizing business values, deciding the
// initial status/priority defaults, verifying the Client exists (DATA-002), translating the wire
// CreateProjectViewModel into the Project aggregate and the one EntityMutationAudited fact for the
// mutation, persisting both through IProjectData, and mapping the result into the wire
// ProjectServiceModel (ProjectContractMappingExtensions). No EF, cache, HttpContext, or Service
// Bus dependency - those belong to Data, Facade, and the outbox relay respectively.
public sealed class ProjectBusiness : IProjectBusiness
{
    private readonly IProjectData _projectData;

    public ProjectBusiness(IProjectData projectData)
    {
        _projectData = projectData ?? throw new ArgumentNullException(nameof(projectData));
    }

    public async Task<ProjectServiceModel> CreateAsync(
        CreateProjectViewModel request,
        ActorContext actor,
        RequestContext requestContext,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedName = NormalizeRequired(request.Name, nameof(request.Name));
        var normalizedOwnerUserId = NormalizeRequired(request.OwnerUserId, nameof(request.OwnerUserId));

        // PROJECT-010: initial status defaults to Planned when omitted (CreateProjectViewModel comment
        // documents this contract expectation).
        var status = request.Status is { } statusValue
            ? statusValue.ToCoreStatus()
            : ProjectStatus.Planned;

        // PROJECT-010: initial priority defaults to Normal when omitted (CreateProjectViewModel comment
        // documents this contract expectation).
        var priority = request.Priority is { } priorityValue
            ? priorityValue.ToCorePriority()
            : ProjectPriority.Normal;

        // PROJECT-001: only an identified actor (User or Service) can be attributed as CreatedBy.
        var createdBy = ResolveCreatedBy(actor);

        var project = Project.Create(
            id: Guid.NewGuid(),
            clientId: request.ClientId,
            name: normalizedName,
            status: status,
            priority: priority,
            ownerUserId: normalizedOwnerUserId,
            createdBy: createdBy,
            createdAtUtc: createdAtUtc,
            description: NormalizeOptional(request.Description),
            startDateUtc: request.StartDateUtc,
            targetCompletionDateUtc: request.TargetCompletionDateUtc,
            notes: NormalizeOptional(request.Notes));

        var auditFact = BuildAuditFact(project, actor, requestContext);

        // IProjectData.CreateAsync verifies that the Client exists (DATA-002) and persists
        // the Project and audit fact atomically, or throws ProjectClientNotFoundException if the
        // Client does not exist.
        await _projectData.CreateAsync(project, auditFact, cancellationToken).ConfigureAwait(false);

        return project.ToServiceModel();
    }

    public async Task<PagedResponse<ProjectServiceModel>> ListAsync(
        ListProjectsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = new ProjectListFilter
        {
            Search = NormalizeOptional(request.Search),
            ClientId = request.ClientId ?? Guid.Empty,
            Status = request.Status?.ToCoreStatus(),
            OwnerUserId = NormalizeOptional(request.OwnerUserId),
            Priority = request.Priority?.ToCorePriority(),
            StartDateUtc = request.StartDateUtc,
            TargetCompletionDateUtc = request.TargetCompletionDateUtc,
            // PROJECT-023 default sort: Name ascending - same fallback ProjectRepository.ApplySort
            // applies for an unmatched ProjectListSortField, so "no sort requested" and "an unmapped
            // sort field" never disagree about the default ordering.
            SortBy = request.SortBy?.ToCoreListSortField() ?? ProjectListSortField.Name,
            SortDirection = request.SortDirection?.ToCoreListSortDirection() ?? ProjectListSortDirection.Ascending,
            Page = request.Page,
            PageSize = request.PageSize,
        };

        var result = await _projectData.ListAsync(filter, cancellationToken).ConfigureAwait(false);

        return new PagedResponse<ProjectServiceModel>
        {
            Items = result.Items.Select(project => project.ToServiceModel()).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = request.PageSize > 0
                ? (int)Math.Ceiling(result.TotalCount / (double)request.PageSize)
                : 0,
        };
    }

    public async Task<ProjectDetailServiceModel?> GetDetailAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project Id cannot be empty.", nameof(projectId));
        }

        var detail = await _projectData.GetDetailAsync(projectId, cancellationToken).ConfigureAwait(false);

        return detail?.ToDetailServiceModel();
    }

    public async Task<ProjectServiceModel?> TransitionStatusAsync(
        Guid projectId,
        ProjectStatusContract targetStatus,
        string expectedConcurrencyToken,
        ActorContext actor,
        RequestContext requestContext,
        DateTime transitionedAtUtc,
        bool acknowledgeOpenTasks = false,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project Id cannot be empty.", nameof(projectId));
        }

        ArgumentNullException.ThrowIfNull(expectedConcurrencyToken);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(requestContext);

        var coreTargetStatus = targetStatus.ToCoreStatus();

        var project = await _projectData.GetAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return null;
        }

        // PROJECT-013: when transitioning to Completed, user must acknowledge any open Tasks exist.
        // The Facade/Controller is responsible for fetching the open-task count and presenting it
        // to the user; this Business layer enforces the acknowledgement policy.
        if (coreTargetStatus == ProjectStatus.Completed && !acknowledgeOpenTasks)
        {
            // Return a special result the Facade/Controller can interpret as "need acknowledgement".
            // This check happens before calling Data, so it's a fast validation gate.
            throw new InvalidOperationException(
                "Completing a Project requires explicit acknowledgement. Open Tasks may exist.");
        }

        var modifiedBy = ResolveCreatedBy(actor);
        var auditFact = BuildStatusChangeAuditFact(
            project,
            coreTargetStatus,
            actor,
            requestContext);

        var completionTimestamp = coreTargetStatus == ProjectStatus.Completed
            ? transitionedAtUtc
            : (DateTime?)null;

        await _projectData.TransitionStatusAsync(
            project,
            coreTargetStatus,
            modifiedBy,
            transitionedAtUtc,
            completionTimestamp,
            expectedConcurrencyToken,
            auditFact,
            cancellationToken).ConfigureAwait(false);

        return project.ToServiceModel();
    }

    public async Task<ProjectServiceModel?> ArchiveAsync(
        Guid projectId,
        string expectedConcurrencyToken,
        ActorContext actor,
        RequestContext requestContext,
        DateTime archivedAtUtc,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project Id cannot be empty.", nameof(projectId));
        }

        ArgumentNullException.ThrowIfNull(expectedConcurrencyToken);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(requestContext);

        var project = await _projectData.GetAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return null;
        }

        var modifiedBy = ResolveCreatedBy(actor);
        var auditFact = BuildArchiveAuditFact(project, actor, requestContext);

        await _projectData.ArchiveAsync(
            project,
            modifiedBy,
            archivedAtUtc,
            expectedConcurrencyToken,
            auditFact,
            cancellationToken).ConfigureAwait(false);

        return project.ToServiceModel();
    }

    public async Task<ProjectServiceModel?> EditAsync(
        Guid projectId,
        UpdateProjectViewModel request,
        string expectedConcurrencyToken,
        ActorContext actor,
        RequestContext requestContext,
        DateTime editedAtUtc,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project Id cannot be empty.", nameof(projectId));
        }

        ArgumentNullException.ThrowIfNull(expectedConcurrencyToken);

        var project = await _projectData.GetAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return null;
        }

        var modifiedBy = ResolveCreatedBy(actor);

        // Capture before values for audit trail (AUDIT-002)
        var beforeValues = CaptureBeforeValues(project, request);

        var changedFields = project.Edit(
            name: request.Name,
            description: request.Description,
            priority: request.Priority?.ToCorePriority(),
            ownerUserId: request.OwnerUserId,
            startDateUtc: request.StartDateUtc,
            targetCompletionDateUtc: request.TargetCompletionDateUtc,
            notes: request.Notes,
            modifiedBy: modifiedBy,
            modifiedAtUtc: editedAtUtc);

        if (changedFields.Count == 0)
        {
            return project.ToServiceModel();
        }

        // Capture after values for audit trail (AUDIT-002)
        var afterValues = CaptureAfterValues(project, changedFields);

        var auditFact = BuildEditAuditFact(project, changedFields, beforeValues, afterValues, actor, requestContext);

        await _projectData.EditAsync(
            project,
            modifiedBy,
            editedAtUtc,
            expectedConcurrencyToken,
            auditFact,
            cancellationToken).ConfigureAwait(false);

        return project.ToServiceModel();
    }

    private static EntityMutationAudited BuildStatusChangeAuditFact(
        Project project,
        ProjectStatus newStatus,
        ActorContext actor,
        RequestContext requestContext)
    {
        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Project,
            EntityId = project.Id,
            Action = AuditActions.StatusChanged,
            ActorId = actor.ActorId,
            ActorType = ResolveAuditActorType(actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = new List<string> { nameof(Project.Status) }.AsReadOnly(),
        };
    }

    private static EntityMutationAudited BuildArchiveAuditFact(
        Project project,
        ActorContext actor,
        RequestContext requestContext)
    {
        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Project,
            EntityId = project.Id,
            Action = AuditActions.Archived,
            ActorId = actor.ActorId,
            ActorType = ResolveAuditActorType(actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = new List<string> { nameof(Project.Status) }.AsReadOnly(),
        };
    }

    private static EntityMutationAudited BuildEditAuditFact(
        Project project,
        IReadOnlyList<string> changedFields,
        IReadOnlyDictionary<string, string> beforeValues,
        IReadOnlyDictionary<string, string> afterValues,
        ActorContext actor,
        RequestContext requestContext)
    {
        var safeChangedFields = changedFields
            .Where(field => !AuditSensitiveFieldNames.IsForbidden(field))
            .ToList()
            .AsReadOnly();

        // Filter previous/new values to only include safe fields (AUDIT-008)
        var safePreviousValues = beforeValues
            .Where(kvp => !AuditSensitiveFieldNames.IsForbidden(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var safeNewValues = afterValues
            .Where(kvp => !AuditSensitiveFieldNames.IsForbidden(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(project.LastModifiedAtUtc, TimeSpan.Zero),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Project,
            EntityId = project.Id,
            Action = AuditActions.Updated,
            ActorId = actor.ActorId,
            ActorType = ResolveAuditActorType(actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            ChangedFields = safeChangedFields,
            PreviousValues = safePreviousValues.Count > 0 ? safePreviousValues : null,
            NewValues = safeNewValues.Count > 0 ? safeNewValues : null,
        };
    }

    private static EntityMutationAudited BuildAuditFact(Project project, ActorContext actor, RequestContext requestContext)
    {
        return new EntityMutationAudited
        {
            EventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = new DateTimeOffset(project.CreatedAtUtc, TimeSpan.Zero),
            SourceService = AuditSourceServices.Crm,
            EntityType = AuditEntityTypes.Project,
            EntityId = project.Id,
            Action = AuditActions.Created,
            ActorId = actor.ActorId,
            ActorType = ResolveAuditActorType(actor.ActorType),
            TraceId = requestContext.TraceId,
            CorrelationId = requestContext.CorrelationId,
            CausationId = requestContext.CausationId,
            // Field names only, never values (AUDIT-008) - a Created fact has no "previous" state to
            // disclose, and this microstep does not decide which "new" values are safe to publish.
            ChangedFields = BuildChangedFields(project),
        };
    }

    // Lists the business fields this creation actually populated. Filtered through
    // AuditSensitiveFieldNames defensively, even though none of Project's own field names are
    // sensitive today - the same guard every publisher is expected to apply (AUDIT-008).
    private static IReadOnlyList<string> BuildChangedFields(Project project)
    {
        var fields = new List<string>
        {
            nameof(Project.Name),
            nameof(Project.Status),
            nameof(Project.Priority),
            nameof(Project.OwnerUserId),
            nameof(Project.ClientId),
        };

        AddIfPresent(fields, nameof(Project.Description), project.Description);
        AddIfPresent(fields, nameof(Project.Notes), project.Notes);

        if (project.StartDateUtc.HasValue)
        {
            fields.Add(nameof(Project.StartDateUtc));
        }

        if (project.TargetCompletionDateUtc.HasValue)
        {
            fields.Add(nameof(Project.TargetCompletionDateUtc));
        }

        return fields.Where(field => !AuditSensitiveFieldNames.IsForbidden(field)).ToList();
    }

    private static void AddIfPresent(List<string> fields, string fieldName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(fieldName);
        }
    }

    private static string ResolveCreatedBy(ActorContext actor) =>
        string.IsNullOrWhiteSpace(actor.ActorId)
            ? throw new ArgumentException(
                "Project creation requires an identified actor (User or Service) with a resolved ActorId.",
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

    // Captures the current values of fields that may be modified during an edit operation,
    // for use in audit trail before/after value tracking (AUDIT-002).
    private static IReadOnlyDictionary<string, string> CaptureBeforeValues(
        Project project,
        UpdateProjectViewModel request)
    {
        var beforeValues = new Dictionary<string, string>();

        if (request.Name is not null)
        {
            beforeValues[nameof(Project.Name)] = project.Name;
        }

        if (request.Description is not null)
        {
            if (project.Description is not null)
            {
                beforeValues[nameof(Project.Description)] = project.Description;
            }
        }

        if (request.Priority.HasValue)
        {
            beforeValues[nameof(Project.Priority)] = project.Priority.ToString();
        }

        if (request.OwnerUserId is not null)
        {
            beforeValues[nameof(Project.OwnerUserId)] = project.OwnerUserId;
        }

        if (request.StartDateUtc.HasValue)
        {
            if (project.StartDateUtc.HasValue)
            {
                beforeValues[nameof(Project.StartDateUtc)] = project.StartDateUtc.Value.ToString("O");
            }
        }

        if (request.TargetCompletionDateUtc.HasValue)
        {
            if (project.TargetCompletionDateUtc.HasValue)
            {
                beforeValues[nameof(Project.TargetCompletionDateUtc)] = project.TargetCompletionDateUtc.Value.ToString("O");
            }
        }

        if (request.Notes is not null)
        {
            if (project.Notes is not null)
            {
                beforeValues[nameof(Project.Notes)] = project.Notes;
            }
        }

        return beforeValues;
    }

    // Captures the new values of fields that were changed during an edit operation,
    // for use in audit trail before/after value tracking (AUDIT-002).
    private static IReadOnlyDictionary<string, string> CaptureAfterValues(
        Project project,
        IReadOnlyList<string> changedFields)
    {
        var afterValues = new Dictionary<string, string>();

        foreach (var field in changedFields)
        {
            // Map field names to their current values on the project entity
            var value = field switch
            {
                nameof(Project.Name) => project.Name,
                nameof(Project.Description) => project.Description ?? string.Empty,
                nameof(Project.Priority) => project.Priority.ToString(),
                nameof(Project.OwnerUserId) => project.OwnerUserId,
                nameof(Project.StartDateUtc) => project.StartDateUtc?.ToString("O") ?? string.Empty,
                nameof(Project.TargetCompletionDateUtc) => project.TargetCompletionDateUtc?.ToString("O") ?? string.Empty,
                nameof(Project.Notes) => project.Notes ?? string.Empty,
                _ => string.Empty,
            };

            if (!string.IsNullOrEmpty(value))
            {
                afterValues[field] = value;
            }
        }

        return afterValues;
    }
}
