import type {
  ClientLifecycleStatus,
  ClientDuplicateMatchField,
  Client,
  Priority,
  ProjectStatus,
  TaskItemStatus,
} from '@/api/clients';
import type { BadgeTone } from '@/design-system';

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

/** CLIENT-004: human-readable label for each duplicate-match criterion. */
export const DUPLICATE_MATCH_LABELS: Record<ClientDuplicateMatchField, string> = {
  Name: 'Name',
  PrimaryEmail: 'Email',
  PrimaryPhone: 'Phone',
};

/** CLIENT-030: Badge tone for each Client lifecycle status (text label always carries the meaning). */
export const LIFECYCLE_STATUS_TONES: Record<ClientLifecycleStatus, BadgeTone> = {
  Lead: 'brand',
  Prospect: 'brand',
  Active: 'success',
  OnHold: 'warning',
  Inactive: 'gray',
  Archived: 'gray',
};

/** CLIENT-030/031: human-readable label for each Project status. */
export const PROJECT_STATUS_LABELS: Record<ProjectStatus, string> = {
  Planned: 'Planned',
  Active: 'Active',
  OnHold: 'On Hold',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
  Archived: 'Archived',
};

export const PROJECT_STATUS_TONES: Record<ProjectStatus, BadgeTone> = {
  Planned: 'brand',
  Active: 'success',
  OnHold: 'warning',
  Completed: 'gray',
  Cancelled: 'error',
  Archived: 'gray',
};

/** CLIENT-030/032: human-readable label for each Task status. */
export const TASK_STATUS_LABELS: Record<TaskItemStatus, string> = {
  Backlog: 'Backlog',
  ToDo: 'To Do',
  InProgress: 'In Progress',
  Blocked: 'Blocked',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
};

export const TASK_STATUS_TONES: Record<TaskItemStatus, BadgeTone> = {
  Backlog: 'gray',
  ToDo: 'gray',
  InProgress: 'brand',
  Blocked: 'warning',
  Completed: 'success',
  Cancelled: 'error',
};

/** Shared Project/Task priority scale label and tone. */
export const PRIORITY_LABELS: Record<Priority, string> = {
  Low: 'Low',
  Normal: 'Normal',
  High: 'High',
  Critical: 'Critical',
};

export const PRIORITY_TONES: Record<Priority, BadgeTone> = {
  Low: 'gray',
  Normal: 'brand',
  High: 'warning',
  Critical: 'error',
};
