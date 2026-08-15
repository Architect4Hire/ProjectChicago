import { type FC } from 'react';
import { useNavigate } from 'react-router-dom';
import { Badge, Button, Card, Cluster, Stack } from '@/design-system';
import type { ClientDetailTaskSummary } from '@/api/clients';
import { PRIORITY_LABELS, PRIORITY_TONES, TASK_STATUS_LABELS, TASK_STATUS_TONES } from '../types';

interface ClientTasksSectionProps {
  clientId: string;
  openTasks: ClientDetailTaskSummary[];
  recentlyCompletedTasks: ClientDetailTaskSummary[];
}

/**
 * CLIENT-030/032: open and recently-completed Task summaries belonging to a Client's Projects,
 * plus navigation to the Client's Tasks. Task-detail navigation is deferred - no per-Task detail
 * route exists yet - so rows are summary-only for now.
 */
export const ClientTasksSection: FC<ClientTasksSectionProps> = ({ clientId, openTasks, recentlyCompletedTasks }) => {
  const navigate = useNavigate();

  return (
    <Card>
      <Stack className="gap-5">
        <Cluster className="justify-between">
          <h2 className="text-base font-semibold text-gray-900 dark:text-white">Tasks</h2>
          <Button variant="outline" size="sm" onClick={() => navigate(`/tasks?clientId=${clientId}`)}>
            View all Tasks
          </Button>
        </Cluster>

        <TaskList title="Open Tasks" tasks={openTasks} emptyMessage="No open tasks for this client's projects." />
        <TaskList
          title="Recently Completed Tasks"
          tasks={recentlyCompletedTasks}
          emptyMessage="No recently completed tasks."
          showCompletedDate
        />
      </Stack>
    </Card>
  );
};

interface TaskListProps {
  title: string;
  tasks: ClientDetailTaskSummary[];
  emptyMessage: string;
  showCompletedDate?: boolean;
}

const TaskList: FC<TaskListProps> = ({ title, tasks, emptyMessage, showCompletedDate }) => (
  <div>
    <h3 className="text-sm font-medium text-gray-700 dark:text-gray-300">
      {title} <span className="text-gray-400 dark:text-gray-600">({tasks.length})</span>
    </h3>

    {tasks.length === 0 ? (
      <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">{emptyMessage}</p>
    ) : (
      <ul className="mt-2 flex flex-col gap-2">
        {tasks.map((task) => (
          <li
            key={task.id}
            className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-gray-200 px-3 py-2 dark:border-gray-800"
          >
            <div>
              <span className="text-sm font-medium text-gray-900 dark:text-white">{task.title}</span>
              {showCompletedDate && task.completedAtUtc && (
                <span className="ml-2 text-xs text-gray-500 dark:text-gray-400">
                  Completed {new Date(task.completedAtUtc).toLocaleDateString()}
                </span>
              )}
              {!showCompletedDate && task.dueDateUtc && (
                <span className="ml-2 text-xs text-gray-500 dark:text-gray-400">
                  Due {new Date(task.dueDateUtc).toLocaleDateString()}
                </span>
              )}
            </div>
            <Cluster className="gap-2">
              <Badge tone={PRIORITY_TONES[task.priority]}>{PRIORITY_LABELS[task.priority]}</Badge>
              <Badge tone={TASK_STATUS_TONES[task.status]}>{TASK_STATUS_LABELS[task.status]}</Badge>
            </Cluster>
          </li>
        ))}
      </ul>
    )}
  </div>
);
