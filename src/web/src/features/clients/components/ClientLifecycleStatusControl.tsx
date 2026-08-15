import { useState, type FC } from 'react';
import { Button, Dialog, Field, Select } from '@/design-system';
import { clientsApi } from '@/api/clients';
import type { ClientDetailRecord, ClientLifecycleStatus } from '@/api/clients';
import { ConflictError, ValidationError } from '@/api';
import { LIFECYCLE_STATUSES } from '../types';

interface ClientLifecycleStatusControlProps {
  client: ClientDetailRecord;
  /** Called after a confirmed change and after the user reloads following a conflict; the parent
   * refetches the Client detail so displayed data always comes from the server, never from a
   * locally-applied optimistic guess. */
  onStatusChanged: () => void;
}

// CLIENT-010..015: every non-Archived status may transition to any other non-Archived status
// (ClientLifecycleTransitionRules). Archived is deliberately excluded from this control's target
// options - transitioning into or out of Archived is the dedicated archive/restore action, which
// this feature's scope explicitly excludes ("Do not add archive controls").
const TARGET_STATUSES: ClientLifecycleStatus[] = ['Lead', 'Prospect', 'Active', 'OnHold', 'Inactive'];

export const ClientLifecycleStatusControl: FC<ClientLifecycleStatusControlProps> = ({
  client,
  onStatusChanged,
}) => {
  const [selectedOverride, setSelectedOverride] = useState<ClientLifecycleStatus | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);

  const options = TARGET_STATUSES.filter((status) => status !== client.lifecycleStatus);
  const selected = (selectedOverride && options.includes(selectedOverride) ? selectedOverride : options[0]) ?? null;

  if (client.lifecycleStatus === 'Archived') {
    return (
      <p className="text-sm text-gray-500 dark:text-gray-400">
        This client is archived. Restoring it is a separate action.
      </p>
    );
  }

  const submit = async () => {
    if (!selected) return;

    setIsSaving(true);
    setError(null);
    setConflict(false);

    try {
      await clientsApi.changeLifecycleStatus(client.id, {
        newStatus: selected,
        expectedConcurrencyToken: client.concurrencyToken,
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
      <Field label="Change lifecycle status" error={error ?? undefined}>
        <Select
          value={selected ?? ''}
          invalid={Boolean(error)}
          disabled={isSaving || options.length === 0}
          onChange={(event) => {
            setSelectedOverride(event.target.value as ClientLifecycleStatus);
            setError(null);
          }}
        >
          {options.map((status) => (
            <option key={status} value={status}>
              {LIFECYCLE_STATUSES[status]}
            </option>
          ))}
        </Select>
      </Field>

      <Button
        type="button"
        variant="outline"
        onClick={() => setConfirmOpen(true)}
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
        title="Confirm lifecycle status change"
        description={
          selected
            ? `Change ${client.name}'s status from ${LIFECYCLE_STATUSES[client.lifecycleStatus]} to ${LIFECYCLE_STATUSES[selected]}?`
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
