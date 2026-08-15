import { type FC } from 'react';
import { useNavigate } from 'react-router-dom';
import type { Client } from '@/api/clients';
import { LIFECYCLE_STATUSES } from '../types';

interface ClientsTableProps {
  clients: Client[];
  sortBy: 'name' | 'createdDate' | 'lastModifiedDate' | 'lifecycleStatus';
  sortDirection: 'Ascending' | 'Descending';
  onSortChange: (sortBy: ClientsTableProps['sortBy'], direction: 'asc' | 'desc') => void;
}

export const ClientsTable: FC<ClientsTableProps> = ({
  clients,
  sortBy,
  sortDirection,
  onSortChange,
}) => {
  const navigate = useNavigate();

  const handleSort = (column: ClientsTableProps['sortBy']) => {
    if (sortBy === column) {
      onSortChange(column, sortDirection === 'Ascending' ? 'desc' : 'asc');
    } else {
      onSortChange(column, 'asc');
    }
  };

  const SortHeader: FC<{ column: ClientsTableProps['sortBy']; label: string }> = ({ column, label }) => (
    <th
      className="cursor-pointer select-none px-4 py-3 text-left text-sm font-semibold text-gray-900 dark:text-white hover:bg-gray-50 dark:hover:bg-gray-900"
      onClick={() => handleSort(column)}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          handleSort(column);
        }
      }}
      aria-label={`Sort by ${label}`}
    >
      <div className="flex items-center gap-2">
        {label}
        {sortBy === column && (
          <span aria-hidden="true">{sortDirection === 'Ascending' ? '↑' : '↓'}</span>
        )}
      </div>
    </th>
  );

  const handleRowClick = (clientId: string) => {
    navigate(`/clients/${clientId}`);
  };

  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse">
        <thead className="border-b border-gray-200 bg-gray-50 dark:border-gray-800 dark:bg-gray-900/50">
          <tr>
            <SortHeader column="name" label="Client Name" />
            <th className="px-4 py-3 text-left text-sm font-semibold text-gray-900 dark:text-white">
              Contact
            </th>
            <th className="px-4 py-3 text-left text-sm font-semibold text-gray-900 dark:text-white">
              Email
            </th>
            <SortHeader column="lifecycleStatus" label="Status" />
            <th className="px-4 py-3 text-left text-sm font-semibold text-gray-900 dark:text-white">
              Owner
            </th>
            <SortHeader column="createdDate" label="Created" />
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-200 dark:divide-gray-800">
          {clients.map((client) => (
            <tr
              key={client.id}
              onClick={() => handleRowClick(client.id)}
              className="cursor-pointer transition-colors hover:bg-gray-50 dark:hover:bg-gray-900/30"
              role="button"
              tabIndex={0}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  handleRowClick(client.id);
                }
              }}
            >
              <td className="px-4 py-3 text-sm text-gray-900 dark:text-white">
                <div className="font-medium">{client.name}</div>
              </td>
              <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-400">
                {client.primaryContactName}
              </td>
              <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-400">
                <a
                  href={`mailto:${client.primaryEmail}`}
                  onClick={(e) => e.stopPropagation()}
                  className="text-brand-600 hover:underline dark:text-brand-400"
                >
                  {client.primaryEmail}
                </a>
              </td>
              <td className="px-4 py-3 text-sm">
                <span
                  className={`inline-flex items-center rounded-full px-2 py-1 text-xs font-medium ${
                    client.lifecycleStatus === 'Active'
                      ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
                      : client.lifecycleStatus === 'Archived'
                        ? 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-400'
                        : client.lifecycleStatus === 'OnHold'
                          ? 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400'
                          : 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400'
                  }`}
                  role="status"
                >
                  {LIFECYCLE_STATUSES[client.lifecycleStatus]}
                </span>
              </td>
              <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-400">
                {client.assignedOwner}
              </td>
              <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-400">
                {new Date(client.createdDate).toLocaleDateString()}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};
