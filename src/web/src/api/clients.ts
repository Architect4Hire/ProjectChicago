import { getGatewayClient } from './gateway';
import type { RequestOptions } from './http';

/**
 * Lifecycle status for a Client
 * Represents the current stage in the Client's relationship with the organization
 */
export type ClientLifecycleStatus = 'Lead' | 'Prospect' | 'Active' | 'OnHold' | 'Inactive' | 'Archived';

/**
 * Client entity representing an organization or individual in business relationship
 * CLIENT-002: Required Client information fields
 */
export interface Client {
  /** Unique internal identifier (CLIENT-001) */
  id: string;
  /** Client name, searchable (CLIENT-003) */
  name: string;
  /** Primary contact person name */
  primaryContactName: string;
  /** Primary contact email, searchable and filterable */
  primaryEmail: string;
  /** Primary contact phone, searchable and filterable */
  primaryPhone: string;
  /** Client website URL */
  website: string;
  /** Street address */
  address: string;
  /** City/locality */
  city: string;
  /** State or province */
  state: string;
  /** Postal code */
  postalCode: string;
  /** Country */
  country: string;
  /** Current lifecycle status (CLIENT-010) */
  lifecycleStatus: ClientLifecycleStatus;
  /** Description or notes */
  description: string;
  /** Assigned owner user ID or name */
  assignedOwner: string;
  /** Creation timestamp (CLIENT-002) */
  createdDate: string;
  /** User who created the record */
  createdBy: string;
  /** Last modification timestamp */
  lastModifiedDate: string;
  /** User who last modified the record */
  lastModifiedBy: string;
  /** Likely-duplicate matches found during creation (CLIENT-004); empty when none */
  possibleDuplicates?: ClientDuplicateWarning[];
}

/**
 * Which CLIENT-004 duplicate-detection criterion matched an existing Client.
 * Mirrors the backend's ClientDuplicateMatchField stable string enum.
 */
export type ClientDuplicateMatchField = 'Name' | 'PrimaryEmail' | 'PrimaryPhone';

/**
 * One likely-duplicate match surfaced by POST /api/clients (CLIENT-004).
 * Duplicate detection warns rather than blocks or silently merges: it rides
 * alongside the created Client instead of a separate blocking status code.
 */
export interface ClientDuplicateWarning {
  clientId: string;
  name: string;
  matchedOn: ClientDuplicateMatchField[];
}

/**
 * Project lifecycle status. Mirrors the backend's ProjectStatusContract stable string enum.
 */
export type ProjectStatus = 'Planned' | 'Active' | 'OnHold' | 'Completed' | 'Cancelled' | 'Archived';

/**
 * Shared priority scale for Projects and Tasks. Mirrors the backend's ProjectPriorityContract /
 * TaskItemPriorityContract stable string enums (identical value sets).
 */
export type Priority = 'Low' | 'Normal' | 'High' | 'Critical';
export type ProjectPriority = Priority;
export type TaskItemPriority = Priority;

/**
 * Task lifecycle status. Mirrors the backend's TaskItemStatusContract stable string enum.
 */
export type TaskItemStatus = 'Backlog' | 'ToDo' | 'InProgress' | 'Blocked' | 'Completed' | 'Cancelled';

/**
 * Full Client entity as returned within a Client detail response (GET /api/clients/{clientId}).
 * Mirrors the backend's ClientServiceModel field names exactly (CLIENT-030) - distinct from the
 * `Client` list/create-response shape above, which is a separate, independently-evolving contract.
 */
export interface ClientDetailRecord {
  id: string;
  name: string;
  primaryContactName?: string | null;
  primaryEmail?: string | null;
  primaryPhone?: string | null;
  website?: string | null;
  addressLine?: string | null;
  city?: string | null;
  stateOrProvince?: string | null;
  postalCode?: string | null;
  country?: string | null;
  lifecycleStatus: ClientLifecycleStatus;
  description?: string | null;
  ownerUserId: string;
  createdAtUtc: string;
  createdBy: string;
  lastModifiedAtUtc: string;
  lastModifiedBy: string;
  concurrencyToken: string;
}

/**
 * One Project's summary within a Client's detail view (CLIENT-030/CLIENT-031). Mirrors the
 * backend's ClientProjectSummary - enough to display and navigate, not the full Project record.
 */
export interface ClientDetailProjectSummary {
  id: string;
  name: string;
  status: ProjectStatus;
  priority: ProjectPriority;
  ownerUserId: string;
  startDateUtc?: string | null;
  targetCompletionDateUtc?: string | null;
  actualCompletionDateUtc?: string | null;
  lastModifiedAtUtc: string;
}

/**
 * One Task's summary within a Client's detail view (CLIENT-030/CLIENT-032). Mirrors the backend's
 * ClientTaskSummary. Carries `projectId` so a Task shown here can navigate to its owning Project.
 */
export interface ClientDetailTaskSummary {
  id: string;
  projectId: string;
  title: string;
  status: TaskItemStatus;
  priority: TaskItemPriority;
  assignedUserId?: string | null;
  dueDateUtc?: string | null;
  completedAtUtc?: string | null;
}

/**
 * Consolidated Client detail response (CLIENT-030..032). Mirrors the backend's
 * ClientDetailServiceModel returned by GET /api/clients/{clientId}. Recent activity and audit
 * history are deliberately absent here - the backend defers those to the Audit Service's own
 * HTTP API (ADR-0016), fetched separately by the audit API module.
 */
export interface ClientDetail {
  client: ClientDetailRecord;
  activeProjects: ClientDetailProjectSummary[];
  historicalProjects: ClientDetailProjectSummary[];
  openTasks: ClientDetailTaskSummary[];
  recentlyCompletedTasks: ClientDetailTaskSummary[];
}

/**
 * Request to create a new Client
 * CLIENT-001: Allow authorized user to create Client
 * Field names/requiredness mirror the backend's CreateClientViewModel contract:
 * only `name` and `ownerUserId` are required, the rest are optional.
 */
export interface CreateClientRequest {
  name: string;
  ownerUserId: string;
  primaryContactName?: string;
  primaryEmail?: string;
  primaryPhone?: string;
  website?: string;
  addressLine?: string;
  city?: string;
  stateOrProvince?: string;
  postalCode?: string;
  country?: string;
  description?: string;
}

/**
 * Request to update an existing Client
 */
export interface UpdateClientRequest {
  name?: string;
  primaryContactName?: string;
  primaryEmail?: string;
  primaryPhone?: string;
  website?: string;
  address?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  country?: string;
  lifecycleStatus?: ClientLifecycleStatus;
  description?: string;
  assignedOwner?: string;
}

/**
 * Request to change a Client's current lifecycle status
 * PATCH /api/clients/{clientId}/lifecycle-status
 * Mirrors the backend's ChangeClientLifecycleStatusViewModel exactly (CLIENT-010..012).
 * ExpectedConcurrencyToken is required (DATA-008): the caller's last-known
 * ClientDetailRecord.concurrencyToken, so a stale write is rejected with 409 rather than silently
 * overwriting a change made by someone else.
 */
export interface ChangeClientLifecycleStatusRequest {
  newStatus: ClientLifecycleStatus;
  expectedConcurrencyToken: string;
}

/**
 * Request to archive a Client
 * POST /api/clients/{clientId}/archive
 * Mirrors the backend's ArchiveClientViewModel exactly (CLIENT-013..015).
 * ExpectedConcurrencyToken is required (DATA-008): the caller's last-known
 * ClientDetailRecord.concurrencyToken, so a stale write is rejected with 409 rather than silently
 * overwriting a change made by someone else.
 */
export interface ArchiveClientRequest {
  expectedConcurrencyToken: string;
}

/**
 * Request to restore an archived Client back to an active lifecycle status
 * POST /api/clients/{clientId}/restore
 * Mirrors the backend's RestoreClientViewModel exactly (CLIENT-013..014).
 * RestoredStatus is required - the caller must explicitly choose which lifecycle status to
 * restore to (e.g. Active, Lead, Prospect) rather than defaulting; Archived is rejected by the
 * server as a restore target.
 * ExpectedConcurrencyToken is required (DATA-008), same reasoning as ArchiveClientRequest.
 */
export interface RestoreClientRequest {
  restoredStatus: ClientLifecycleStatus;
  expectedConcurrencyToken: string;
}

/**
 * Sort order options for Client list
 * CLIENT-023: Sort by name, created date, modified date, lifecycle status
 */
export type ClientSortBy = 'name' | 'createdDate' | 'lastModifiedDate' | 'lifecycleStatus';

export type SortDirection = 'Ascending' | 'Descending';

/**
 * Filter criteria for Client list
 * CLIENT-022: Filter by lifecycle status and assigned owner
 */
export interface ClientListFilter {
  /** Search query applied to name, contact name, email, phone */
  search?: string;
  /** Filter by lifecycle status (CLIENT-022) */
  lifecycleStatus?: ClientLifecycleStatus | ClientLifecycleStatus[];
  /** Filter by assigned owner (CLIENT-022) */
  assignedOwner?: string;
  /** Exclude archived clients from results (CLIENT-013) */
  excludeArchived?: boolean;
}

/**
 * Pagination options for Client list
 * CLIENT-020, CLIENT-024: Paginated list with bounded results
 */
export interface ClientListOptions extends ClientListFilter {
  /** Page number (1-indexed) */
  pageNumber?: number;
  /** Page size (CLIENT-024: bounded results) */
  pageSize?: number;
  /** Sort column (CLIENT-023) */
  sortBy?: ClientSortBy;
  /** Sort direction */
  sortDirection?: SortDirection;
}

/**
 * Paginated response for Client list
 * API-005: Collection endpoints provide pagination
 */
export interface PagedResponse<T> {
  /** Current page number (1-indexed) */
  pageNumber: number;
  /** Number of items per page */
  pageSize: number;
  /** Total number of items matching criteria */
  totalCount: number;
  /** Total number of pages */
  totalPages: number;
  /** Items on current page */
  items: T[];
}

/**
 * Client API operations
 * Follows REST conventions (API-001, API-002, API-003)
 */
export const clientsApi = {
  /**
   * List Clients with pagination, search, filter, and sort
   * GET /api/clients
   * CLIENT-020: View paginated Client list
   * CLIENT-021: Search by name, contact name, email, phone
   * CLIENT-022: Filter by status and owner
   * CLIENT-023: Sort by name, created date, modified date, status
   * CLIENT-024: Server-side pagination
   */
  async listClients(options?: ClientListOptions): Promise<PagedResponse<Client>> {
    const client = getGatewayClient();

    const params = new URLSearchParams();

    if (options?.pageNumber) params.append('pageNumber', options.pageNumber.toString());
    if (options?.pageSize) params.append('pageSize', options.pageSize.toString());
    if (options?.search) params.append('search', options.search);
    if (options?.lifecycleStatus) {
      if (Array.isArray(options.lifecycleStatus)) {
        options.lifecycleStatus.forEach(status => params.append('lifecycleStatus', status));
      } else {
        params.append('lifecycleStatus', options.lifecycleStatus);
      }
    }
    if (options?.assignedOwner) params.append('assignedOwner', options.assignedOwner);
    if (options?.excludeArchived !== undefined) {
      params.append('excludeArchived', options.excludeArchived.toString());
    }
    if (options?.sortBy) params.append('sortBy', options.sortBy);
    if (options?.sortDirection) params.append('sortDirection', options.sortDirection);

    const queryString = params.toString();
    const url = queryString ? `/api/clients?${queryString}` : '/api/clients';

    return client.get<PagedResponse<Client>>(url);
  },

  /**
   * Get consolidated Client detail by ID
   * GET /api/clients/{clientId}
   * CLIENT-030..032: Client detail experience with consolidated view (Client info, lifecycle,
   * owner, active/historical Projects, open/recently-completed Tasks). Recent activity and audit
   * history are fetched separately through the audit API module (ADR-0016).
   */
  async getClient(clientId: string, options?: RequestOptions): Promise<ClientDetail> {
    const client = getGatewayClient();
    return client.get<ClientDetail>(`/api/clients/${clientId}`, options);
  },

  /**
   * Change a Client's current lifecycle status
   * PATCH /api/clients/{clientId}/lifecycle-status
   * CLIENT-010..012: a Client has exactly one current status and transitions are auditable
   * (recorded through the Audit Service via the normal integration-event path, not by this call).
   * Rejects with ValidationError (400) when the transition itself is disallowed, and with
   * ConflictError (409) when expectedConcurrencyToken no longer matches the persisted Client -
   * callers must reload rather than retry blindly (DATA-008).
   * Response shape mirrors the backend's ClientServiceModel for the fields ClientDetailRecord also
   * carries; PossibleDuplicates (irrelevant to a lifecycle transition) is simply ignored.
   */
  async changeLifecycleStatus(
    clientId: string,
    request: ChangeClientLifecycleStatusRequest,
  ): Promise<ClientDetailRecord> {
    const client = getGatewayClient();
    return client.patch<ClientDetailRecord, ChangeClientLifecycleStatusRequest>(
      `/api/clients/${clientId}/lifecycle-status`,
      request,
    );
  },

  /**
   * Create a new Client
   * POST /api/clients
   * CLIENT-001: Authorized user can create Client
   * CLIENT-002: Client with required and optional fields
   */
  async createClient(request: CreateClientRequest): Promise<Client> {
    const client = getGatewayClient();
    return client.post<Client, CreateClientRequest>('/api/clients', request);
  },

  /**
   * Update an existing Client
   * PUT /api/clients/{clientId}
   * API-003: PUT for update operations
   */
  async updateClient(clientId: string, request: UpdateClientRequest): Promise<Client> {
    const client = getGatewayClient();
    return client.put<Client, UpdateClientRequest>(`/api/clients/${clientId}`, request);
  },

  /**
   * Archive a Client
   * POST /api/clients/{clientId}/archive
   * CLIENT-013: Archived clients not in normal lists
   * CLIENT-014: Archiving doesn't delete historical information
   * CLIENT-015: Clients with active projects cannot be permanently removed - the server rejects
   * with ConflictError (409) when the Client has active Projects, and also when
   * expectedConcurrencyToken no longer matches the persisted Client (DATA-008).
   * Response shape mirrors the backend's ClientServiceModel for the fields ClientDetailRecord also
   * carries; PossibleDuplicates (irrelevant to archiving) is simply ignored.
   */
  async archiveClient(clientId: string, request: ArchiveClientRequest): Promise<ClientDetailRecord> {
    const client = getGatewayClient();
    return client.post<ClientDetailRecord, ArchiveClientRequest>(`/api/clients/${clientId}/archive`, request);
  },

  /**
   * Restore an archived Client to a chosen non-Archived lifecycle status
   * POST /api/clients/{clientId}/restore
   * CLIENT-013: Restoring makes the Client eligible for normal active lists again
   * CLIENT-014: Restore is the counterpart to non-destructive archiving
   * Rejects with ValidationError (400) when the Client is not currently Archived or
   * restoredStatus is itself Archived, and with ConflictError (409) when
   * expectedConcurrencyToken no longer matches the persisted Client (DATA-008).
   * Response shape mirrors the backend's ClientServiceModel for the fields ClientDetailRecord also
   * carries; PossibleDuplicates (irrelevant to restoring) is simply ignored.
   */
  async restoreClient(clientId: string, request: RestoreClientRequest): Promise<ClientDetailRecord> {
    const client = getGatewayClient();
    return client.post<ClientDetailRecord, RestoreClientRequest>(`/api/clients/${clientId}/restore`, request);
  },
};
