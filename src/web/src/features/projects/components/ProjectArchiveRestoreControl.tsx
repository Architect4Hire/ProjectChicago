import { useState, type FC } from 'react';
import { Button, Dialog, Field, Select } from '@/design-system';
import { projectsApi } from '@/api/projects';
import type { ProjectDetailRecord, ProjectStatus } from '@/api/projects';
import { ConflictError, ValidationError } from '@/api';
import { useAuth } from '@/auth';
import { PROJECT_STATUS_LABELS } from '../types';

interface ProjectArchiveRestoreControlProps {
  project: ProjectDetailRecord;
  /** Called after a confirmed archive/restore; the parent refetches the Project detail so displayed
   * data always comes from the server, never from a locally-applied optimistic guess. */
  onChanged: () => void;
}

// Roles granted the backend's Projects.Write policy. Used only as a
// client-side hint to avoid showing a Restore control that will predictably 403 - the backend
// policy is still the actual security control (security.md: guards are navigation aids, not
// enforcement). Archive intentionally is NOT gated the same way: it stays visible and relies
// on the server 403 for enforcement.
const PROJECTS_WRITE_ROLES = ['Administrator', 'Manager'];

// PROJECT-014: restoring an Archived Project requires the caller to explicitly choose a
// non-Archived destination status.
const RESTORE_TARGET_STATUSES: ProjectStatus[] = ['Planned', 'Active', 'OnHold', 'Cancelled'];

export const ProjectArchiveRestoreControl: FC<ProjectArchiveRestoreControlProps> = ({
  project,
  onChanged,
}) => {
  if (project.status === 'Archived') {
    return <RestoreControl project={project} onChanged={onChanged} />;
  }

  return <ArchiveControl project={project} onChanged={onChanged} />;
};

const ArchiveControl: FC<{
  project: ProjectDetailRecord;
  onChanged: () => void;
}> = ({ project, onChanged }) => {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);

  const openDialog = () => {
    setError(null);
    setConflict(false);
    setDialogOpen(true);
  };

  const closeDialog = () => {
    if (isSaving) return;
    setDialogOpen(false);
  };

  const submit = async () => {
    setIsSaving(true);
    setError(null);
    setConflict(false);

    try {
      await projectsApi.archiveProject(project.id, { expectedConcurrencyToken: project.concurrencyToken });
      setDialogOpen(false);
      onChanged();
    } catch (err) {
      setDialogOpen(false);

      if (err instanceof ConflictError) {
        setConflict(true);
      } else if (err instanceof ValidationError) {
        setError(err.problemDetails.detail || 'This project could not be archived.');
      } else {
        setError('The project could not be archived. Try again.');
      }
    } finally {
      setIsSaving(false);
    }
  };

  const reload = () => {
    setConflict(false);
    onChanged();
  };

  return (
    <div className="flex flex-col items-start gap-3 sm:flex-row sm:items-center">
      <Button type="button" variant="danger" onClick={openDialog}>
        Archive project
      </Button>

      {error && (
        <p role="alert" className="text-sm text-error-600 dark:text-error-400">
          {error}
        </p>
      )}

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

      <Dialog
        open={dialogOpen}
        onClose={closeDialog}
        title="Archive this project?"
        description={`${project.name} will no longer appear in normal active Project lists. Its historical information is kept, and it can be restored later (PROJECT-014).`}
        actions={
          <>
            <Button type="button" variant="outline" onClick={closeDialog} disabled={isSaving}>
              Cancel
            </Button>
            <Button type="button" variant="danger" onClick={submit} isLoading={isSaving}>
              Archive
            </Button>
          </>
        }
      />
    </div>
  );
};

const RestoreControl: FC<{ project: ProjectDetailRecord; onChanged: () => void }> = ({ project, onChanged }) => {
  const { currentUser } = useAuth();
  const isAuthorized = (currentUser?.roles ?? []).some((role) => PROJECTS_WRITE_ROLES.includes(role));

  const [selectedOverride, setSelectedOverride] = useState<ProjectStatus | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);

  const selected = selectedOverride && RESTORE_TARGET_STATUSES.includes(selectedOverride)
    ? selectedOverride
    : RESTORE_TARGET_STATUSES[0];

  if (!isAuthorized) {
    return (
      <p className="text-sm text-gray-500 dark:text-gray-400">
        This project is archived. Restoring it requires Administrator or Manager access.
      </p>
    );
  }

  const submit = async () => {
    setIsSaving(true);
    setError(null);
    setConflict(false);

    try {
      await projectsApi.restoreProject(project.id, {
        restoredStatus: selected,
        expectedConcurrencyToken: project.concurrencyToken,
      });
      setConfirmOpen(false);
      onChanged();
    } catch (err) {
      setConfirmOpen(false);

      if (err instanceof ConflictError) {
        setConflict(true);
      } else if (err instanceof ValidationError) {
        setError(err.fieldErrors.restoredStatus?.[0] || 'This project could not be restored.');
      } else {
        setError('The project could not be restored. Try again.');
      }
    } finally {
      setIsSaving(false);
    }
  };

  const reload = () => {
    setConflict(false);
    onChanged();
  };

  return (
    <div className="flex flex-col items-start gap-3 sm:flex-row sm:items-end">
      <Field label="Restore to status" error={error ?? undefined}>
        <Select
          value={selected}
          invalid={Boolean(error)}
          disabled={isSaving}
          onChange={(event) => {
            setSelectedOverride(event.target.value as ProjectStatus);
            setError(null);
          }}
        >
          {RESTORE_TARGET_STATUSES.map((status) => (
            <option key={status} value={status}>
              {PROJECT_STATUS_LABELS[status]}
            </option>
          ))}
        </Select>
      </Field>

      <Button type="button" onClick={() => setConfirmOpen(true)} disabled={isSaving} isLoading={isSaving}>
        Restore project
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

      <Dialog
        open={confirmOpen}
        onClose={() => {
          if (!isSaving) setConfirmOpen(false);
        }}
        title="Confirm project restore"
        description={`Restore ${project.name} from Archived to ${PROJECT_STATUS_LABELS[selected]}?`}
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
