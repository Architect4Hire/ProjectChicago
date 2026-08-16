import { type FC } from 'react';
import { Card, Stack } from '@/design-system';
import type { ProjectDetailTaskSummary } from '@/api/projects';

interface ProjectTasksSectionProps {
  openTasks: ProjectDetailTaskSummary[];
  completedTasks: ProjectDetailTaskSummary[];
}

export const ProjectTasksSection: FC<ProjectTasksSectionProps> = ({ openTasks, completedTasks }) => {
  const formatDate = (dateStr: string | null | undefined): string => {
    if (!dateStr) return '-';
    try {
      const date = new Date(dateStr);
      return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    } catch {
      return dateStr;
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Backlog':
        return 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200';
      case 'ToDo':
        return 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200';
      case 'InProgress':
        return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200';
      case 'Blocked':
        return 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200';
      case 'Completed':
        return 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200';
      case 'Cancelled':
        return 'bg-gray-200 text-gray-700 dark:bg-gray-700 dark:text-gray-300';
      default:
        return 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200';
    }
  };

  return (
    <Stack className="gap-4">
      <Card>
        <Stack className="gap-3">
          <div>
            <h3 className="text-base font-semibold text-gray-900 dark:text-white">
              Open Tasks ({openTasks.length})
            </h3>
          </div>

          {openTasks.length === 0 ? (
            <p className="text-sm text-gray-500 dark:text-gray-400">No open tasks</p>
          ) : (
            <div className="space-y-2">
              {openTasks.map((task) => (
                <div key={task.id} className="flex items-center justify-between rounded bg-gray-50 p-3 dark:bg-gray-900">
                  <div className="flex-1">
                    <p className="text-sm font-medium text-gray-900 dark:text-white">{task.title}</p>
                    <div className="mt-1 flex items-center gap-2">
                      <span className={`inline-block rounded px-2 py-1 text-xs font-medium ${getStatusColor(task.status)}`}>
                        {task.status}
                      </span>
                      {task.priority && (
                        <span className="text-xs text-gray-600 dark:text-gray-400">{task.priority}</span>
                      )}
                      {task.dueDateUtc && (
                        <span className="text-xs text-gray-600 dark:text-gray-400">Due: {formatDate(task.dueDateUtc)}</span>
                      )}
                    </div>
                  </div>
                  {task.assignedUserId && (
                    <span className="text-xs text-gray-600 dark:text-gray-400">{task.assignedUserId}</span>
                  )}
                </div>
              ))}
            </div>
          )}
        </Stack>
      </Card>

      {completedTasks.length > 0 && (
        <Card>
          <Stack className="gap-3">
            <div>
              <h3 className="text-base font-semibold text-gray-900 dark:text-white">
                Completed Tasks ({completedTasks.length})
              </h3>
            </div>

            <div className="space-y-2">
              {completedTasks.map((task) => (
                <div
                  key={task.id}
                  className="flex items-center justify-between rounded bg-gray-50 p-3 dark:bg-gray-900 opacity-75"
                >
                  <div className="flex-1">
                    <p className="text-sm font-medium text-gray-900 line-through dark:text-white">{task.title}</p>
                    <div className="mt-1 flex items-center gap-2">
                      <span className="inline-block rounded bg-green-100 px-2 py-1 text-xs font-medium text-green-800 dark:bg-green-900 dark:text-green-200">
                        Completed
                      </span>
                      {task.completedAtUtc && (
                        <span className="text-xs text-gray-600 dark:text-gray-400">
                          Completed: {formatDate(task.completedAtUtc)}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </Stack>
        </Card>
      )}
    </Stack>
  );
};
