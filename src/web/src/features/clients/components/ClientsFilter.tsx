import { type FC } from 'react';
import { Stack } from '@/design-system';
import type { ClientLifecycleStatus } from '@/api/clients';
import { LIFECYCLE_STATUSES } from '../types';

interface ClientsFilterProps {
  search: string;
  onSearchChange: (search: string) => void;
  lifecycleStatus: ClientLifecycleStatus[];
  onLifecycleStatusChange: (status: ClientLifecycleStatus[]) => void;
  assignedOwner: string;
  onAssignedOwnerChange: (owner: string) => void;
  excludeArchived: boolean;
  onExcludeArchivedChange: (exclude: boolean) => void;
}

export const ClientsFilter: FC<ClientsFilterProps> = ({
  search,
  onSearchChange,
  lifecycleStatus,
  onLifecycleStatusChange,
  assignedOwner,
  onAssignedOwnerChange,
  excludeArchived,
  onExcludeArchivedChange,
}) => {
  const handleStatusChange = (status: ClientLifecycleStatus) => {
    if (lifecycleStatus.includes(status)) {
      onLifecycleStatusChange(lifecycleStatus.filter((s) => s !== status));
    } else {
      onLifecycleStatusChange([...lifecycleStatus, status]);
    }
  };

  return (
    <Stack className="gap-4 rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-950">
      <div>
        <label htmlFor="search-input" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Search
        </label>
        <input
          id="search-input"
          type="text"
          placeholder="Search by name, contact, email, or phone"
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm placeholder-gray-400 shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-900 dark:text-white dark:placeholder-gray-500"
          aria-label="Search clients"
        />
      </div>

      <div>
        <fieldset>
          <legend className="text-sm font-medium text-gray-700 dark:text-gray-300">Lifecycle Status</legend>
          <div className="mt-2 space-y-2">
            {Object.entries(LIFECYCLE_STATUSES).map(([key, label]) => (
              <label key={key} className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={lifecycleStatus.includes(key as ClientLifecycleStatus)}
                  onChange={() => handleStatusChange(key as ClientLifecycleStatus)}
                  className="rounded border-gray-300 text-brand-600 focus:ring-brand-500"
                />
                <span className="text-sm text-gray-700 dark:text-gray-300">{label}</span>
              </label>
            ))}
          </div>
        </fieldset>
      </div>

      <div>
        <label htmlFor="owner-input" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Assigned Owner
        </label>
        <input
          id="owner-input"
          type="text"
          placeholder="Filter by owner name or ID"
          value={assignedOwner}
          onChange={(e) => onAssignedOwnerChange(e.target.value)}
          className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm placeholder-gray-400 shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-900 dark:text-white dark:placeholder-gray-500"
          aria-label="Filter by assigned owner"
        />
      </div>

      <label className="flex items-center gap-2">
        <input
          type="checkbox"
          checked={excludeArchived}
          onChange={(e) => onExcludeArchivedChange(e.target.checked)}
          className="rounded border-gray-300 text-brand-600 focus:ring-brand-500"
        />
        <span className="text-sm text-gray-700 dark:text-gray-300">Exclude archived clients</span>
      </label>
    </Stack>
  );
};
