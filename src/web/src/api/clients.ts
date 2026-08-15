import { getGatewayClient } from './gateway';

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
}

/**
 * Request to create a new Client
 * CLIENT-001: Allow authorized user to create Client
 */
export interface CreateClientRequest {
  name: string;
  primaryContactName: string;
  primaryEmail: string;
  primaryPhone: string;
  website?: string;
  address?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  country?: string;
  description?: string;
  assignedOwner?: string;
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
 * Sort order options for Client list
 * CLIENT-023: Sort by name, created date, modified date, lifecycle status
 */
export type ClientSortBy = 'name' | 'createdDate' | 'lastModifiedDate' | 'lifecycleStatus';

export type SortDirection = 'asc' | 'desc';

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
   * Get a single Client by ID
   * GET /api/clients/{clientId}
   * CLIENT-030: Client detail experience with consolidated view
   */
  async getClient(clientId: string): Promise<Client> {
    const client = getGatewayClient();
    return client.get<Client>(`/api/clients/${clientId}`);
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
   * DELETE /api/clients/{clientId}
   * API-003: DELETE for archive operations
   * CLIENT-013: Archived clients not in normal lists
   * CLIENT-014: Archiving doesn't delete historical information
   * CLIENT-015: Clients with active projects cannot be permanently removed
   */
  async archiveClient(clientId: string): Promise<void> {
    const client = getGatewayClient();
    await client.delete(`/api/clients/${clientId}`);
  },
};
