import { type FC } from 'react';
import { Stack } from '@/design-system';
import type { ProjectStatus, ProjectPriority } from '@/api/projects';

interface ProjectsFilterProps {
  search: string;
  onSearchChange: (search: string) => void;
  clientId: string;
  onClientIdChange: (clientId: string) => void;
  status: ProjectStatus[];
  onStatusChange: (status: ProjectStatus[]) => void;
  ownerUserId: string;
  onOwnerUserIdChange: (ownerUserId: string) => void;
  priority: ProjectPriority[];
  onPriorityChange: (priority: ProjectPriority[]) => void;
  startDateFromUtc: string;
  startDateToUtc: string;
  onStartDateRangeChange: (fromUtc: string, toUtc: string) => void;
  targetCompletionDateFromUtc: string;
  targetCompletionDateToUtc: string;
  onTargetCompletionDateRangeChange: (fromUtc: string, toUtc: string) => void;
  excludeArchived: boolean;
  onExcludeArchivedChange: (exclude: boolean) => void;
}

export const ProjectsFilter: FC<ProjectsFilterProps> = ({
  search,
  onSearchChange,
  clientId,
  onClientIdChange,
  status,
  onStatusChange,
  ownerUserId,
  onOwnerUserIdChange,
  priority,
  onPriorityChange,
  startDateFromUtc,
  startDateToUtc,
  onStartDateRangeChange,
  targetCompletionDateFromUtc,
  targetCompletionDateToUtc,
  onTargetCompletionDateRangeChange,
  excludeArchived,
  onExcludeArchivedChange,
}) => {
  const projectStatuses: ProjectStatus[] = ['Planned', 'Active', 'OnHold', 'Completed', 'Cancelled', 'Archived'];
  const projectPriorities: ProjectPriority[] = ['Low', 'Normal', 'High', 'Critical'];

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-950">
      <Stack className="gap-4">
        <div>
          <label htmlFor="search-input" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
            Search
          </label>
          <input
            id="search-input"
            type="text"
            placeholder="Search by project name, client name, or description..."
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm placeholder-gray-400 shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-900 dark:text-white dark:placeholder-gray-500"
            aria-label="Search projects"
          />
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <label htmlFor="client-id-input" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
              Client ID
            </label>
            <input
              id="client-id-input"
              type="text"
              placeholder="Filter by client..."
              value={clientId}
              onChange={(e) => onClientIdChange(e.target.value)}
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm placeholder-gray-400 shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-900 dark:text-white dark:placeholder-gray-500"
              aria-label="Filter by client ID"
            />
          </div>

          <div>
            <label htmlFor="owner-input" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
              Owner
            </label>
            <input
              id="owner-input"
              type="text"
              placeholder="Filter by owner..."
              value={ownerUserId}
              onChange={(e) => onOwnerUserIdChange(e.target.value)}
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm placeholder-gray-400 shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-900 dark:text-white dark:placeholder-gray-500"
              aria-label="Filter by owner user ID"
            />
          </div>

          <div>
            <label htmlFor="start-date-from-input" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
              Start Date From
            </label>
            <input
              id="start-date-from-input"
              type="date"
              value={startDateFromUtc}
              onChange={(e) => onStartDateRangeChange(e.target.value, startDateToUtc)}
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-900 dark:text-white"
              aria-label="Filter by start date from"
            />
          </div>

          <div>
            <label htmlFor="start-date-to-input" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
              Start Date To
            </label>
            <input
              id="start-date-to-input"
              type="date"
              value={startDateToUtc}
              onChange={(e) => onStartDateRangeChange(startDateFromUtc, e.target.value)}
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-900 dark:text-white"
              aria-label="Filter by start date to"
            />
          </div>

          <div>
            <label htmlFor="target-date-from-input" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
              Target Completion From
            </label>
            <input
              id="target-date-from-input"
              type="date"
              value={targetCompletionDateFromUtc}
              onChange={(e) => onTargetCompletionDateRangeChange(e.target.value, targetCompletionDateToUtc)}
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-900 dark:text-white"
              aria-label="Filter by target completion date from"
            />
          </div>

          <div>
            <label htmlFor="target-date-to-input" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
              Target Completion To
            </label>
            <input
              id="target-date-to-input"
              type="date"
              value={targetCompletionDateToUtc}
              onChange={(e) => onTargetCompletionDateRangeChange(targetCompletionDateFromUtc, e.target.value)}
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-900 dark:text-white"
              aria-label="Filter by target completion date to"
            />
          </div>
        </div>

        <div className="border-t border-gray-200 pt-4 dark:border-gray-800">
          <div className="space-y-3">
            <div>
              <label className="text-sm font-medium text-gray-900 dark:text-white">Status</label>
              <div className="mt-2 space-y-2">
                {projectStatuses.map((s) => (
                  <label key={s} className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      checked={status.includes(s)}
                      onChange={(e) => {
                        if (e.target.checked) {
                          onStatusChange([...status, s]);
                        } else {
                          onStatusChange(status.filter((x) => x !== s));
                        }
                      }}
                      className="h-4 w-4 rounded border-gray-300"
                      aria-label={`Filter by status ${s}`}
                    />
                    <span className="text-sm text-gray-700 dark:text-gray-300">{s}</span>
                  </label>
                ))}
              </div>
            </div>

            <div>
              <label className="text-sm font-medium text-gray-900 dark:text-white">Priority</label>
              <div className="mt-2 space-y-2">
                {projectPriorities.map((p) => (
                  <label key={p} className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      checked={priority.includes(p)}
                      onChange={(e) => {
                        if (e.target.checked) {
                          onPriorityChange([...priority, p]);
                        } else {
                          onPriorityChange(priority.filter((x) => x !== p));
                        }
                      }}
                      className="h-4 w-4 rounded border-gray-300"
                      aria-label={`Filter by priority ${p}`}
                    />
                    <span className="text-sm text-gray-700 dark:text-gray-300">{p}</span>
                  </label>
                ))}
              </div>
            </div>

            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="exclude-archived"
                checked={excludeArchived}
                onChange={(e) => onExcludeArchivedChange(e.target.checked)}
                className="h-4 w-4 rounded border-gray-300"
              />
              <label htmlFor="exclude-archived" className="text-sm text-gray-700 dark:text-gray-300">
                Exclude archived projects
              </label>
            </div>
          </div>
        </div>
      </Stack>
    </div>
  );
};
