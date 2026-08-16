import type { ProjectStatus, ProjectPriority, Project, ProjectDetail } from '@/api/projects';
import type { BadgeTone } from '@/design-system';

export const DEFAULT_PAGE_SIZE = 10;

/** PROJECT-010: human-readable label for each Project status. */
export const PROJECT_STATUS_LABELS: Record<ProjectStatus, string> = {
  Planned: 'Planned',
  Active: 'Active',
  OnHold: 'On Hold',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
  Archived: 'Archived',
};

/** PROJECT-030: Badge tone for each Project status (text label always carries the meaning). */
export const PROJECT_STATUS_TONES: Record<ProjectStatus, BadgeTone> = {
  Planned: 'brand',
  Active: 'success',
  OnHold: 'warning',
  Completed: 'gray',
  Cancelled: 'error',
  Archived: 'gray',
};

export type ProjectsSortBy = 'name' | 'status' | 'priority' | 'createdDate' | 'startDate' | 'targetCompletionDate';

export interface ProjectListState {
  projects: Project[];
  isLoading: boolean;
  error: string | null;
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  search: string;
  clientId: string;
  status: ProjectStatus[];
  ownerUserId: string;
  priority: ProjectPriority[];
  startDateFromUtc: string;
  startDateToUtc: string;
  targetCompletionDateFromUtc: string;
  targetCompletionDateToUtc: string;
  excludeArchived: boolean;
  sortBy: ProjectsSortBy;
  sortDirection: 'Ascending' | 'Descending';
}

export interface ProjectDetailState {
  detail: ProjectDetail | null;
  isLoading: boolean;
  error: string | null;
  notFound: boolean;
}
