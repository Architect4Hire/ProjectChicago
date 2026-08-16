import { useState, type FC } from 'react';
import { Button, Dialog, Field, Select } from '@/design-system';
import { projectsApi } from '@/api/projects';
import type { ProjectDetailRecord, ProjectStatus } from '@/api/projects';
import { ConflictError, ValidationError } from '@/api';
import { PROJECT_STATUS_LABELS } from '../types';

interface ProjectStatusControlProps {
  project: ProjectDetailRecord;
  openTasksCount: number;
  /** Called after a confirmed status change and after the user reloads following a conflict;
   * the parent refetches the Project detail so displayed data always comes from the server. */
  onStatusChanged: () => void;
}

// PROJECT-010: status transitions exclude Archived (handled by archive/restore control).
// Non-Archived statuses can transition to any other non-Archived status.
const TARGET_STATUSES: ProjectStatus[] = ['Planned', 'Active', 'OnHold', 'Completed', 'Cancelled'];

export const ProjectStatusControl: FC<ProjectStatusControlProps> = ({
  project,
  openTasksCount,
  onStatusChanged,
}) => {
  const [selectedOverride, setSelectedOverride] = useState<ProjectStatus | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [showTaskAckDialog, setShowTaskAckDialog] = useState(false);

  // Archived projects don't show this control
  if (project.status === 'Archived') {
    return (
      <p className="text-sm text-gray-500 dark:text-gray-400">
        This project is archived. Restoring it is a separate action.
      </p>
    );
  }

  const options = TARGET_STATUSES.filter((status) => status !== project.status);
  const selected = (selectedOverride && options.includes(selectedOverride) ? selectedOverride : options[0]) ?? null;

  const handleStatusClick = () => {
    setError(null);
    setConflict(false);

    // PROJECT-013: If transitioning to Completed with open tasks, show acknowledgement dialog
    if (selected === 'Completed' && openTasksCount > 0) {
      setShowTaskAckDialog(true);
      return;
    }

    setConfirmOpen(true);
  };

  const handleAcknowledgeAndContinue = () => {
    setShowTaskAckDialog(false);
    setConfirmOpen(true);
  };

  const submit = async () => {
    if (!selected) return;

    setIsSaving(true);
    setError(null);
    setConflict(false);

    try {
      await projectsApi.changeStatus(project.id, {
        newStatus: selected,
        expectedConcurrencyToken: project.concurrencyToken,
        acknowledgeOpenTasks: selected === 'Completed' && openTasksCount > 0,
      });
      setConfirmOpen(false);
      onStatusChanged();
    } catch (err) {
      setConfirmOpen(false);

      if (err instanceof ConflictError) {
        setConflict(true);
      } else if (err instanceof ValidationError) {
        setError(err.fieldErrors.newStatus?.[0] || 'This status change is not allowed.');
      } else {
        setError('The status change could not be saved. Try again.');
      }
    } finally {
      setIsSaving(false);
    }
  };

  const reload = () => {
    setConflict(false);
    onStatusChanged();
  };

  return (
    <div className="flex flex-col items-start gap-3 sm:flex-row sm:items-end">
      <Field label="Change status" error={error ?? undefined}>
        <Select
          value={selected ?? ''}
          invalid={Boolean(error)}
          disabled={isSaving || options.length === 0}
          onChange={(event) => {
            setSelectedOverride(event.target.value as ProjectStatus);
            setError(null);
          }}
        >
          {options.map((status) => (
            <option key={status} value={status}>
              {PROJECT_STATUS_LABELS[status]}
            </option>
          ))}
        </Select>
      </Field>

      <Button
        type="button"
        variant="outline"
        onClick={handleStatusClick}
        disabled={isSaving || options.length === 0}
        isLoading={isSaving}
      >
        Change status
      </Button>

      {conflict && (
        <div
          role="alert"
          className="flex flex-wrap items-center gap-2 rounded-lg border border-warning-300 bg-warning-50 px-3 py-2 text-sm text-warning-700 dark:border-warning-800 dark:bg-warning-900/20 dark:text-warning-400"
        >
          <span>Someone else changed this project. Reload to see the latest status before trying again.</span>
          <Button type="button" variant="ghost" size="sm" onClick={reload}>
            Reload
          </Button>
        </div>
      )}

      {/* PROJECT-013: Acknowledgement dialog for open tasks before Completed status */}
      <Dialog
        open={showTaskAckDialog}
        onClose={() => {
          if (!isSaving) setShowTaskAckDialog(false);
        }}
        title="Open tasks remain"
        description={`This project has ${openTasksCount} open task${openTasksCount > 1 ? 's' : ''}. You can mark the project as Completed while tasks remain open, but they must be addressed separately. Do you want to continue?`}
        actions={
          <>
            <Button
              type="button"
              variant="outline"
              onClick={() => setShowTaskAckDialog(false)}
              disabled={isSaving}
            >
              Cancel
            </Button>
            <Button type="button" onClick={handleAcknowledgeAndContinue} isLoading={isSaving}>
              Continue to completion
            </Button>
          </>
        }
      />

      <Dialog
        open={confirmOpen}
        onClose={() => {
          if (!isSaving) setConfirmOpen(false);
        }}
        title="Confirm status change"
        description={
          selected
            ? `Change ${project.name}'s status from ${PROJECT_STATUS_LABELS[project.status]} to ${PROJECT_STATUS_LABELS[selected]}?`
            : undefined
        }
        actions={
          <>
            <Button type="button" variant="outline" onClick={() => setConfirmOpen(false)} disabled={isSaving}>
              Cancel
            </Button>
            <Button type="button" onClick={submit} isLoading={isSaving}>
              Confirm
            </Button>
          </>
        }
      />
    </div>
  );
};
