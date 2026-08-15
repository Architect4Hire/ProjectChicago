import { useState, type FC } from 'react';
import { Button, Dialog, Field, Select } from '@/design-system';
import { clientsApi } from '@/api/clients';
import type { ClientDetailRecord, ClientLifecycleStatus } from '@/api/clients';
import { ConflictError, ValidationError } from '@/api';
import { useAuth } from '@/auth';
import { LIFECYCLE_STATUSES } from '../types';

interface ClientArchiveRestoreControlProps {
  client: ClientDetailRecord;
  /** True when the Client has any active Project (CLIENT-015 blocks archiving in that case). */
  hasActiveProjects: boolean;
  /** Called after a confirmed archive/restore; the parent refetches the Client detail so displayed
   * data always comes from the server, never from a locally-applied optimistic guess. */
  onChanged: () => void;
}

// Roles granted the backend's Clients.Write policy (ProjectChicago.Crm Program.cs). Used only as a
// client-side hint to avoid showing a Restore control that will predictably 403 - the backend
// policy is still the actual security control (security.md: guards are navigation aids, not
// enforcement). Archive intentionally is NOT gated the same way: it stays visible like
// ClientLifecycleStatusControl's "Change status" action and relies on the server 403 for
// enforcement, so a denied attempt is explained rather than the control silently disappearing.
const CLIENTS_WRITE_ROLES = ['Administrator', 'Manager'];

// CLIENT-010..015: restoring an Archived Client requires the caller to explicitly choose a
// non-Archived destination status (RestoreClientViewModel.RestoredStatus is required server-side).
const RESTORE_TARGET_STATUSES: ClientLifecycleStatus[] = ['Lead', 'Prospect', 'Active', 'OnHold', 'Inactive'];

export const ClientArchiveRestoreControl: FC<ClientArchiveRestoreControlProps> = ({
  client,
  hasActiveProjects,
  onChanged,
}) => {
  if (client.lifecycleStatus === 'Archived') {
    return <RestoreControl client={client} onChanged={onChanged} />;
  }

  return <ArchiveControl client={client} hasActiveProjects={hasActiveProjects} onChanged={onChanged} />;
};

const ArchiveControl: FC<{
  client: ClientDetailRecord;
  hasActiveProjects: boolean;
  onChanged: () => void;
}> = ({ client, hasActiveProjects, onChanged }) => {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [blocked, setBlocked] = useState(false);

  const openDialog = () => {
    setError(null);
    setConflict(false);
    setBlocked(hasActiveProjects);
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
      await clientsApi.archiveClient(client.id, { expectedConcurrencyToken: client.concurrencyToken });
      setDialogOpen(false);
      onChanged();
    } catch (err) {
      if (err instanceof ConflictError) {
        setDialogOpen(false);

        // The Client acquired an active Project between page load and this submit - CLIENT-015
        // blocks the archive the same way the up-front hasActiveProjects check does.
        if (err.problemDetails.detail?.includes('active Projects')) {
          setBlocked(true);
          setDialogOpen(true);
        } else {
          setConflict(true);
        }
      } else if (err instanceof ValidationError) {
        setError(err.problemDetails.detail || 'This client could not be archived.');
      } else {
        setError('The client could not be archived. Try again.');
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
        Archive client
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
          <span>Someone else changed this client. Reload to see the latest status before trying again.</span>
          <Button type="button" variant="ghost" size="sm" onClick={reload}>
            Reload
          </Button>
        </div>
      )}

      {blocked ? (
        <Dialog
          open={dialogOpen}
          onClose={closeDialog}
          title="This client cannot be archived"
          description={`${client.name} has active Projects. Complete, cancel, or reassign them before archiving this client (CLIENT-015).`}
          actions={
            <Button type="button" variant="outline" onClick={closeDialog}>
              Close
            </Button>
          }
        />
      ) : (
        <Dialog
          open={dialogOpen}
          onClose={closeDialog}
          title="Archive this client?"
          description={`${client.name} will no longer appear in normal active Client lists. Its historical information is kept, and it can be restored later (CLIENT-013/CLIENT-014).`}
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
      )}
    </div>
  );
};

const RestoreControl: FC<{ client: ClientDetailRecord; onChanged: () => void }> = ({ client, onChanged }) => {
  const { currentUser } = useAuth();
  const isAuthorized = (currentUser?.roles ?? []).some((role) => CLIENTS_WRITE_ROLES.includes(role));

  const [selectedOverride, setSelectedOverride] = useState<ClientLifecycleStatus | null>(null);
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
        This client is archived. Restoring it requires Administrator or Manager access.
      </p>
    );
  }

  const submit = async () => {
    setIsSaving(true);
    setError(null);
    setConflict(false);

    try {
      await clientsApi.restoreClient(client.id, {
        restoredStatus: selected,
        expectedConcurrencyToken: client.concurrencyToken,
      });
      setConfirmOpen(false);
      onChanged();
    } catch (err) {
      setConfirmOpen(false);

      if (err instanceof ConflictError) {
        setConflict(true);
      } else if (err instanceof ValidationError) {
        setError(err.fieldErrors.restoredStatus?.[0] || 'This client could not be restored.');
      } else {
        setError('The client could not be restored. Try again.');
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
            setSelectedOverride(event.target.value as ClientLifecycleStatus);
            setError(null);
          }}
        >
          {RESTORE_TARGET_STATUSES.map((status) => (
            <option key={status} value={status}>
              {LIFECYCLE_STATUSES[status]}
            </option>
          ))}
        </Select>
      </Field>

      <Button type="button" onClick={() => setConfirmOpen(true)} disabled={isSaving} isLoading={isSaving}>
        Restore client
      </Button>

      {conflict && (
        <div
          role="alert"
          className="flex flex-wrap items-center gap-2 rounded-lg border border-warning-300 bg-warning-50 px-3 py-2 text-sm text-warning-700 dark:border-warning-800 dark:bg-warning-900/20 dark:text-warning-400"
        >
          <span>Someone else changed this client. Reload to see the latest status before trying again.</span>
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
        title="Confirm client restore"
        description={`Restore ${client.name} from Archived to ${LIFECYCLE_STATUSES[selected]}?`}
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
