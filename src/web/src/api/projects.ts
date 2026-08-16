import { getGatewayClient } from './gateway';
import type { RequestOptions } from './http';
import type { PagedResponse, Priority, SortDirection, ProjectStatus } from './clients';

// Re-export ProjectStatus for consumers
export type { ProjectStatus };

/**
 * Shared priority scale for Projects
 * Re-exported from clients.ts for consistency across entities
 */
export type ProjectPriority = Priority;

/**
 * Project entity representing a body of work performed for a Client
 * PROJECT-002: Required Project information fields
 * Every Project belongs to exactly one Client
 */
export interface Project {
  /** Unique internal identifier (PROJECT-001) */
  id: string;
  /** Client ID this project belongs to (DATA-002: Project must have a Client) */
  clientId: string;
  /** Client name (searchable per PROJECT-022) */
  clientName: string;
  /** Project name, searchable (PROJECT-022) */
  name: string;
  /** Project description, searchable (PROJECT-022) */
  description: string;
  /** Current project status (PROJECT-010) */
  status: ProjectStatus;
  /** Project priority (PROJECT-021: filterable) */
  priority: ProjectPriority;
  /** Project owner user ID */
  ownerUserId: string;
  /** Project start date UTC (PROJECT-021: filterable) */
  startDateUtc?: string | null;
  /** Target completion date UTC (PROJECT-021: filterable) */
  targetCompletionDateUtc?: string | null;
  /** Actual completion date UTC (PROJECT-012: captured when completed) */
  actualCompletionDateUtc?: string | null;
  /** Project notes */
  notes: string;
  /** Creation timestamp (PROJECT-002) */
  createdAtUtc: string;
  /** User who created the project */
  createdBy: string;
  /** Last modification timestamp */
  lastModifiedAtUtc: string;
  /** User who last modified the project */
  lastModifiedBy: string;
}

/**
 * Full Project entity as returned in project detail response (GET /api/projects/{projectId})
 * PROJECT-030: Project detail shows complete project information
 * Includes concurrency token for optimistic concurrency control (DATA-008)
 */
export interface ProjectDetailRecord {
  id: string;
  clientId: string;
  clientName: string;
  name: string;
  description?: string | null;
  status: ProjectStatus;
  priority: ProjectPriority;
  ownerUserId: string;
  startDateUtc?: string | null;
  targetCompletionDateUtc?: string | null;
  actualCompletionDateUtc?: string | null;
  notes?: string | null;
  createdAtUtc: string;
  createdBy: string;
  lastModifiedAtUtc: string;
  lastModifiedBy: string;
  concurrencyToken: string;
}

/**
 * One Task summary within a Project's detail view (PROJECT-030/PROJECT-031)
 * Mirrors the backend's ProjectTaskSummary - carries enough to display and navigate
 */
export interface ProjectDetailTaskSummary {
  id: string;
  title: string;
  status: 'Backlog' | 'ToDo' | 'InProgress' | 'Blocked' | 'Completed' | 'Cancelled';
  priority: ProjectPriority;
  assignedUserId?: string | null;
  dueDateUtc?: string | null;
  completedAtUtc?: string | null;
}

/**
 * Consolidated Project detail response (PROJECT-030)
 * Mirrors the backend's ProjectDetailServiceModel returned by GET /api/projects/{projectId}
 * Recent activity and audit history are fetched separately (ADR-0016)
 */
export interface ProjectDetail {
  project: ProjectDetailRecord;
  openTasks: ProjectDetailTaskSummary[];
  completedTasks: ProjectDetailTaskSummary[];
}

/**
 * Request to create a new Project
 * PROJECT-001: Authorized users can create a Project for a Client
 * PROJECT-002: Project with required and optional fields
 * Field names/requiredness mirror the backend's CreateProjectViewModel
 */
export interface CreateProjectRequest {
  clientId: string;
  name: string;
  ownerUserId: string;
  description?: string;
  priority?: ProjectPriority;
  startDateUtc?: string;
  targetCompletionDateUtc?: string;
  notes?: string;
}

/**
 * Request to update an existing Project
 * API-003: PUT for update operations
 */
export interface UpdateProjectRequest {
  name?: string;
  description?: string;
  priority?: ProjectPriority;
  ownerUserId?: string;
  startDateUtc?: string;
  targetCompletionDateUtc?: string;
  notes?: string;
}

/**
 * Request to change a Project's current status
 * PATCH /api/projects/{projectId}/status
 * PROJECT-010..013: Project status transitions
 * PROJECT-012: Completing a project captures actual completion timestamp
 * PROJECT-013: Open tasks require acknowledgement before marking completed
 * ExpectedConcurrencyToken required (DATA-008) for optimistic concurrency control
 */
export interface ChangeProjectStatusRequest {
  newStatus: ProjectStatus;
  expectedConcurrencyToken: string;
  acknowledgeOpenTasks?: boolean;
}

/**
 * Request to archive a Project
 * POST /api/projects/{projectId}/archive
 * PROJECT-014: Projects not physically deleted, instead archived (non-destructive archival)
 * ExpectedConcurrencyToken required (DATA-008)
 */
export interface ArchiveProjectRequest {
  expectedConcurrencyToken: string;
}

/**
 * Request to restore an archived Project
 * POST /api/projects/{projectId}/restore
 * PROJECT-014: Restoring makes archived project available again
 * RestoredStatus required - caller explicitly chooses which lifecycle status to restore to
 * ExpectedConcurrencyToken required (DATA-008)
 */
export interface RestoreProjectRequest {
  restoredStatus: ProjectStatus;
  expectedConcurrencyToken: string;
}

/**
 * Sort order options for Project list
 * PROJECT-023: Sort by commonly used attributes
 */
export type ProjectSortBy = 'name' | 'status' | 'priority' | 'startDate' | 'targetCompletionDate' | 'createdDate' | 'lastModifiedDate';

/**
 * Sort direction for projects
 * Uses SortDirection from clients.ts for consistency
 */
export type ProjectSortDirection = SortDirection;

/**
 * Filter criteria for Project list
 * PROJECT-021: Filter by client, status, owner, priority, start/target completion date
 */
export interface ProjectListFilter {
  /** Search query applied to project name, client name, description (PROJECT-022) */
  search?: string;
  /** Filter by client ID (PROJECT-021) */
  clientId?: string;
  /** Filter by project status (PROJECT-021) */
  status?: ProjectStatus | ProjectStatus[];
  /** Filter by project owner (PROJECT-021) */
  ownerUserId?: string;
  /** Filter by project priority (PROJECT-021) */
  priority?: ProjectPriority | ProjectPriority[];
  /** Filter by start date range - minimum start date UTC (PROJECT-021) */
  startDateFromUtc?: string;
  /** Filter by start date range - maximum start date UTC (PROJECT-021) */
  startDateToUtc?: string;
  /** Filter by target completion date range - minimum date UTC (PROJECT-021) */
  targetCompletionDateFromUtc?: string;
  /** Filter by target completion date range - maximum date UTC (PROJECT-021) */
  targetCompletionDateToUtc?: string;
  /** Exclude archived projects from results (PROJECT-014) */
  excludeArchived?: boolean;
}

/**
 * Pagination options for Project list
 * PROJECT-020, PROJECT-023: Paginated list with bounded results
 */
export interface ProjectListOptions extends ProjectListFilter {
  /** Page number (1-indexed) */
  pageNumber?: number;
  /** Page size (PROJECT-023: bounded results) */
  pageSize?: number;
  /** Sort column (PROJECT-023) */
  sortBy?: ProjectSortBy;
  /** Sort direction */
  sortDirection?: ProjectSortDirection;
}

/**
 * Project API operations
 * Follows REST conventions (API-001, API-002, API-003)
 */
export const projectsApi = {
  /**
   * List Projects with pagination, search, filter, and sort
   * GET /api/projects
   * PROJECT-020: View projects across clients, for specific client, assigned to owner
   * PROJECT-021: Filter by client, status, owner, priority, dates
   * PROJECT-022: Search by project name, client name, description
   * PROJECT-023: Server-side pagination
   */
  async listProjects(options?: ProjectListOptions): Promise<PagedResponse<Project>> {
    const client = getGatewayClient();

    const params = new URLSearchParams();

    if (options?.pageNumber) params.append('pageNumber', options.pageNumber.toString());
    if (options?.pageSize) params.append('pageSize', options.pageSize.toString());
    if (options?.search) params.append('search', options.search);
    if (options?.clientId) params.append('clientId', options.clientId);
    if (options?.status) {
      if (Array.isArray(options.status)) {
        options.status.forEach(status => params.append('status', status));
      } else {
        params.append('status', options.status);
      }
    }
    if (options?.ownerUserId) params.append('ownerUserId', options.ownerUserId);
    if (options?.priority) {
      if (Array.isArray(options.priority)) {
        options.priority.forEach(p => params.append('priority', p));
      } else {
        params.append('priority', options.priority);
      }
    }
    if (options?.startDateFromUtc) params.append('startDateFromUtc', options.startDateFromUtc);
    if (options?.startDateToUtc) params.append('startDateToUtc', options.startDateToUtc);
    if (options?.targetCompletionDateFromUtc) params.append('targetCompletionDateFromUtc', options.targetCompletionDateFromUtc);
    if (options?.targetCompletionDateToUtc) params.append('targetCompletionDateToUtc', options.targetCompletionDateToUtc);
    if (options?.excludeArchived !== undefined) {
      params.append('excludeArchived', options.excludeArchived.toString());
    }
    if (options?.sortBy) params.append('sortBy', options.sortBy);
    if (options?.sortDirection) params.append('sortDirection', options.sortDirection);

    const queryString = params.toString();
    const url = queryString ? `/api/projects?${queryString}` : '/api/projects';

    return client.get<PagedResponse<Project>>(url);
  },

  /**
   * Get consolidated Project detail by ID
   * GET /api/projects/{projectId}
   * PROJECT-030: Project detail view with project info, client, status, owner, priority,
   * important dates, open/completed tasks. Recent activity and audit history are fetched
   * separately through the audit API module (ADR-0016).
   */
  async getProject(projectId: string, options?: RequestOptions): Promise<ProjectDetail> {
    const client = getGatewayClient();
    return client.get<ProjectDetail>(`/api/projects/${projectId}`, options);
  },

  /**
   * Change a Project's current status
   * PATCH /api/projects/{projectId}/status
   * PROJECT-010..013: Project has exactly one current status, transitions generate audit records
   * PROJECT-012: Completing a project captures actual completion timestamp
   * PROJECT-013: Completing with open tasks requires acknowledgeOpenTasks flag
   * Rejects with ValidationError (400) when transition is disallowed or acknowledgement needed,
   * and with ConflictError (409) when expectedConcurrencyToken doesn't match (DATA-008).
   * Response shape mirrors ProjectDetailRecord for the fields that need updating.
   */
  async changeStatus(
    projectId: string,
    request: ChangeProjectStatusRequest,
  ): Promise<ProjectDetailRecord> {
    const client = getGatewayClient();
    return client.patch<ProjectDetailRecord, ChangeProjectStatusRequest>(
      `/api/projects/${projectId}/status`,
      request,
    );
  },

  /**
   * Create a new Project
   * POST /api/projects
   * PROJECT-001: Authorized user can create Project
   * PROJECT-002: Project with required and optional fields
   * Mirrors backend's CreateProjectViewModel
   */
  async createProject(request: CreateProjectRequest): Promise<Project> {
    const client = getGatewayClient();
    return client.post<Project, CreateProjectRequest>('/api/projects', request);
  },

  /**
   * Update an existing Project
   * PUT /api/projects/{projectId}
   * API-003: PUT for update operations
   */
  async updateProject(projectId: string, request: UpdateProjectRequest): Promise<Project> {
    const client = getGatewayClient();
    return client.put<Project, UpdateProjectRequest>(`/api/projects/${projectId}`, request);
  },

  /**
   * Archive a Project
   * POST /api/projects/{projectId}/archive
   * PROJECT-014: Projects not deleted, archived instead (non-destructive archival)
   * Archived projects remain available for audit, reporting, historical relationships
   * Rejects with ConflictError (409) when expectedConcurrencyToken no longer matches
   * the persisted Project (DATA-008).
   * Response shape mirrors ProjectDetailRecord for consistency.
   */
  async archiveProject(projectId: string, request: ArchiveProjectRequest): Promise<ProjectDetailRecord> {
    const client = getGatewayClient();
    return client.post<ProjectDetailRecord, ArchiveProjectRequest>(`/api/projects/${projectId}/archive`, request);
  },

  /**
   * Restore an archived Project to a chosen non-Archived status
   * POST /api/projects/{projectId}/restore
   * PROJECT-014: Restoring makes archived project eligible for normal lists again
   * Rejects with ValidationError (400) when Project is not Archived or
   * restoredStatus is itself Archived, and with ConflictError (409) when
   * expectedConcurrencyToken no longer matches (DATA-008).
   * Response shape mirrors ProjectDetailRecord for consistency.
   */
  async restoreProject(projectId: string, request: RestoreProjectRequest): Promise<ProjectDetailRecord> {
    const client = getGatewayClient();
    return client.post<ProjectDetailRecord, RestoreProjectRequest>(`/api/projects/${projectId}/restore`, request);
  },
};
