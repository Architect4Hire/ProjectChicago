import type { ClientLifecycleStatus, Client } from '@/api/clients';

export interface ClientListState {
  clients: Client[];
  isLoading: boolean;
  error: string | null;
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  search: string;
  lifecycleStatus: ClientLifecycleStatus[];
  assignedOwner: string;
  excludeArchived: boolean;
  sortBy: 'name' | 'createdDate' | 'lastModifiedDate' | 'lifecycleStatus';
  sortDirection: 'Ascending' | 'Descending';
}

export const DEFAULT_PAGE_SIZE = 20;

export const LIFECYCLE_STATUSES: Record<ClientLifecycleStatus, string> = {
  Lead: 'Lead',
  Prospect: 'Prospect',
  Active: 'Active',
  OnHold: 'On Hold',
  Inactive: 'Inactive',
  Archived: 'Archived',
};

export const SORT_OPTIONS = [
  { value: 'name', label: 'Name' },
  { value: 'createdDate', label: 'Created Date' },
  { value: 'lastModifiedDate', label: 'Modified Date' },
  { value: 'lifecycleStatus', label: 'Lifecycle Status' },
] as const;
