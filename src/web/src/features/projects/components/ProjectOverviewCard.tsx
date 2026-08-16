import { useState, type FC } from 'react';
import { Button, Card, Stack } from '@/design-system';
import type { ProjectDetailRecord, ProjectDetailTaskSummary } from '@/api/projects';
import { ProjectStatusControl } from './ProjectStatusControl';
import { ProjectArchiveRestoreControl } from './ProjectArchiveRestoreControl';
import { ProjectDetailsEditForm } from './ProjectDetailsEditForm';

interface ProjectOverviewCardProps {
  project: ProjectDetailRecord;
  openTasks?: ProjectDetailTaskSummary[];
  onProjectChanged?: () => void;
}

export const ProjectOverviewCard: FC<ProjectOverviewCardProps> = ({ project, openTasks = [], onProjectChanged }) => {
  const [isEditingDetails, setIsEditingDetails] = useState(false);
  const formatDate = (dateStr: string | null | undefined): string => {
    if (!dateStr) return 'Not set';
    try {
      const date = new Date(dateStr);
      return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
    } catch {
      return dateStr;
    }
  };

  if (isEditingDetails) {
    return (
      <Card>
        <Stack className="gap-4">
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Edit project details</h3>
            <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">
              Update project name, description, priority, dates, and notes. Status and archival are managed separately.
            </p>
          </div>
          <div className="border-t border-gray-200 pt-6 dark:border-gray-800">
            <ProjectDetailsEditForm
              project={project}
              onSaved={() => {
                setIsEditingDetails(false);
                onProjectChanged?.();
              }}
              onCancel={() => setIsEditingDetails(false)}
            />
          </div>
        </Stack>
      </Card>
    );
  }

  return (
    <Card>
      <Stack className="gap-4">
        <div className="flex items-center justify-between">
          <div />
          <Button
            variant="outline"
            size="sm"
            onClick={() => setIsEditingDetails(true)}
          >
            Edit details
          </Button>
        </div>

        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
          <div>
            <p className="text-sm font-medium text-gray-600 dark:text-gray-400">Client</p>
            <p className="mt-1 text-base font-semibold text-gray-900 dark:text-white">{project.clientName}</p>
          </div>

          <div>
            <p className="text-sm font-medium text-gray-600 dark:text-gray-400">Status</p>
            <p className="mt-1 inline-block rounded-full bg-blue-100 px-3 py-1 text-sm font-medium text-blue-800 dark:bg-blue-900 dark:text-blue-200">
              {project.status}
            </p>
          </div>

          <div>
            <p className="text-sm font-medium text-gray-600 dark:text-gray-400">Priority</p>
            <p className="mt-1 text-base font-semibold text-gray-900 dark:text-white">{project.priority}</p>
          </div>

          <div>
            <p className="text-sm font-medium text-gray-600 dark:text-gray-400">Owner</p>
            <p className="mt-1 text-base font-semibold text-gray-900 dark:text-white">{project.ownerUserId || '-'}</p>
          </div>

          <div>
            <p className="text-sm font-medium text-gray-600 dark:text-gray-400">Start Date</p>
            <p className="mt-1 text-base text-gray-900 dark:text-white">{formatDate(project.startDateUtc)}</p>
          </div>

          <div>
            <p className="text-sm font-medium text-gray-600 dark:text-gray-400">Target Completion</p>
            <p className="mt-1 text-base text-gray-900 dark:text-white">{formatDate(project.targetCompletionDateUtc)}</p>
          </div>

          {project.actualCompletionDateUtc && (
            <div>
              <p className="text-sm font-medium text-gray-600 dark:text-gray-400">Actual Completion</p>
              <p className="mt-1 text-base text-gray-900 dark:text-white">{formatDate(project.actualCompletionDateUtc)}</p>
            </div>
          )}
        </div>

        {project.description && (
          <div className="border-t border-gray-200 pt-4 dark:border-gray-800">
            <p className="text-sm font-medium text-gray-600 dark:text-gray-400">Description</p>
            <p className="mt-2 text-base text-gray-900 dark:text-white">{project.description}</p>
          </div>
        )}

        {project.notes && (
          <div className="border-t border-gray-200 pt-4 dark:border-gray-800">
            <p className="text-sm font-medium text-gray-600 dark:text-gray-400">Notes</p>
            <p className="mt-2 text-base text-gray-900 dark:text-white">{project.notes}</p>
          </div>
        )}

        <div className="border-t border-gray-200 pt-4 dark:border-gray-800">
          <p className="text-xs text-gray-500 dark:text-gray-400">
            Created by {project.createdBy} on {formatDate(project.createdAtUtc)} • Last modified by {project.lastModifiedBy}
          </p>
        </div>

        {onProjectChanged && (
          <div className="space-y-6 border-t border-gray-200 pt-6 dark:border-gray-800">
            <div>
              <h3 className="mb-4 text-sm font-semibold text-gray-900 dark:text-white">Status Management</h3>
              <ProjectStatusControl
                project={project}
                openTasksCount={openTasks.length}
                onStatusChanged={onProjectChanged}
              />
            </div>

            <div>
              <h3 className="mb-4 text-sm font-semibold text-gray-900 dark:text-white">Archive</h3>
              <ProjectArchiveRestoreControl project={project} onChanged={onProjectChanged} />
            </div>
          </div>
        )}
      </Stack>
    </Card>
  );
};
