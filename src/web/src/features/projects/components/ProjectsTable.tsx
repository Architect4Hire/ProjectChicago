import { type FC } from 'react';
import { useNavigate } from 'react-router-dom';
import type { Project } from '@/api/projects';

interface ProjectsTableProps {
  projects: Project[];
  sortBy: string;
  sortDirection: 'Ascending' | 'Descending';
  onSortChange: (sortBy: string, direction: 'asc' | 'desc') => void;
}

export const ProjectsTable: FC<ProjectsTableProps> = ({ projects, sortBy, sortDirection, onSortChange }) => {
  const navigate = useNavigate();

  const handleSort = (column: string) => {
    if (sortBy === column) {
      onSortChange(column, sortDirection === 'Ascending' ? 'desc' : 'asc');
    } else {
      onSortChange(column, 'asc');
    }
  };

  const formatDate = (dateStr: string | null | undefined): string => {
    if (!dateStr) return '-';
    try {
      const date = new Date(dateStr);
      return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
    } catch {
      return dateStr;
    }
  };

  const getSortIndicator = (column: string) => {
    if (sortBy !== column) return '';
    return sortDirection === 'Ascending' ? ' ↑' : ' ↓';
  };

  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="border-b border-gray-200 dark:border-gray-800">
          <th className="px-6 py-3 text-left font-semibold text-gray-900 dark:text-white">
            <button
              onClick={() => handleSort('name')}
              className="flex items-center gap-2 hover:text-gray-600 dark:hover:text-gray-400"
            >
              Project Name
              {getSortIndicator('name')}
            </button>
          </th>
          <th className="px-6 py-3 text-left font-semibold text-gray-900 dark:text-white">
            Client
          </th>
          <th className="px-6 py-3 text-left font-semibold text-gray-900 dark:text-white">
            <button
              onClick={() => handleSort('status')}
              className="flex items-center gap-2 hover:text-gray-600 dark:hover:text-gray-400"
            >
              Status
              {getSortIndicator('status')}
            </button>
          </th>
          <th className="px-6 py-3 text-left font-semibold text-gray-900 dark:text-white">
            Owner
          </th>
          <th className="px-6 py-3 text-left font-semibold text-gray-900 dark:text-white">
            <button
              onClick={() => handleSort('priority')}
              className="flex items-center gap-2 hover:text-gray-600 dark:hover:text-gray-400"
            >
              Priority
              {getSortIndicator('priority')}
            </button>
          </th>
          <th className="px-6 py-3 text-left font-semibold text-gray-900 dark:text-white">
            Start Date
          </th>
          <th className="px-6 py-3 text-left font-semibold text-gray-900 dark:text-white">
            Target Date
          </th>
          <th className="px-6 py-3 text-center font-semibold text-gray-900 dark:text-white">
            Tasks
          </th>
        </tr>
      </thead>
      <tbody>
        {projects.map((project) => (
          <tr
            key={project.id}
            onClick={() => navigate(`/projects/${project.id}`)}
            className="cursor-pointer border-b border-gray-200 hover:bg-gray-50 dark:border-gray-800 dark:hover:bg-gray-900"
          >
            <td className="px-6 py-4 text-gray-900 dark:text-white">
              <span className="font-medium hover:underline">{project.name}</span>
            </td>
            <td className="px-6 py-4 text-gray-700 dark:text-gray-300">
              {project.clientName}
            </td>
            <td className="px-6 py-4">
              <span className={`inline-block rounded-full px-3 py-1 text-xs font-medium ${getStatusBadgeClass(project.status)}`}>
                {project.status}
              </span>
            </td>
            <td className="px-6 py-4 text-gray-700 dark:text-gray-300">
              {project.ownerUserId || '-'}
            </td>
            <td className="px-6 py-4">
              <span className={`text-xs font-semibold ${getPriorityClass(project.priority)}`}>
                {project.priority}
              </span>
            </td>
            <td className="px-6 py-4 text-gray-700 dark:text-gray-300">
              {formatDate(project.startDateUtc)}
            </td>
            <td className="px-6 py-4 text-gray-700 dark:text-gray-300">
              {formatDate(project.targetCompletionDateUtc)}
            </td>
            <td className="px-6 py-4 text-center text-gray-700 dark:text-gray-300">
              0
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
};

function getStatusBadgeClass(status: string): string {
  const baseClass = 'px-3 py-1 text-xs font-medium rounded-full';
  switch (status) {
    case 'Planned':
      return `${baseClass} bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200`;
    case 'Active':
      return `${baseClass} bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200`;
    case 'OnHold':
      return `${baseClass} bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200`;
    case 'Completed':
      return `${baseClass} bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200`;
    case 'Cancelled':
      return `${baseClass} bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200`;
    case 'Archived':
      return `${baseClass} bg-gray-200 text-gray-700 dark:bg-gray-700 dark:text-gray-300`;
    default:
      return baseClass;
  }
}

function getPriorityClass(priority: string): string {
  switch (priority) {
    case 'Low':
      return 'text-gray-600 dark:text-gray-400';
    case 'Normal':
      return 'text-blue-600 dark:text-blue-400';
    case 'High':
      return 'text-orange-600 dark:text-orange-400';
    case 'Critical':
      return 'text-red-600 dark:text-red-400';
    default:
      return 'text-gray-600 dark:text-gray-400';
  }
}
