import { type FC } from 'react';
import { Card, EmptyState, ErrorState, Spinner, Stack } from '@/design-system';
import type { AuditEntry } from '@/api/audit';
import type { ClientActivityState } from '../hooks/useClientActivity';

interface ClientActivityPanelProps {
  state: ClientActivityState;
  onRetry: () => void;
}

function describeEntry(entry: AuditEntry): string {
  return entry.summaryDescription || `${entry.action} (${entry.entityType})`;
}

function describeActor(entry: AuditEntry): string {
  if (entry.actorDisplayName) return entry.actorDisplayName;
  switch (entry.actorType) {
    case 'System':
      return 'System';
    case 'Service':
      return entry.sourceService;
    case 'Anonymous':
      return 'Anonymous';
    default:
      return 'Unknown user';
  }
}

function parseChangedFields(changedFields: string): string[] {
  try {
    const parsed: unknown = JSON.parse(changedFields);
    return Array.isArray(parsed) ? parsed.filter((field): field is string => typeof field === 'string') : [];
  } catch {
    return [];
  }
}

/**
 * ACTIVITY-001..003, CLIENT-030: recent activity derived from business audit events, shown with
 * user-friendly descriptions. Each entry discloses its underlying audit fields (source service,
 * trace/correlation IDs, changed field names) via a native <details> element - keyboard-operable
 * without custom ARIA - satisfying "retaining links to underlying audit information where
 * authorized" without inventing a route to a not-yet-built audit detail page.
 */
export const ClientActivityPanel: FC<ClientActivityPanelProps> = ({ state, onRetry }) => {
  return (
    <Card>
      <Stack className="gap-5">
        <h2 className="text-base font-semibold text-gray-900 dark:text-white">Recent activity</h2>

        {state.isLoading && (
          <div className="flex min-h-32 items-center justify-center">
            <Spinner label="Loading activity..." />
          </div>
        )}

        {!state.isLoading && state.error && <ErrorState retry={onRetry} />}

        {!state.isLoading && !state.error && !state.isAuthorized && (
          <EmptyState
            title="Activity not available"
            description="Viewing recent activity and audit history requires Administrator or Manager access."
          />
        )}

        {!state.isLoading && !state.error && state.isAuthorized && state.entries.length === 0 && (
          <EmptyState title="No recent activity" description="No audited events have been recorded for this client yet." />
        )}

        {!state.isLoading && !state.error && state.isAuthorized && state.entries.length > 0 && (
          <Stack className="gap-3">
            <ul className="flex flex-col gap-3">
              {state.entries.map((entry) => (
                <li key={entry.auditEntryId} className="rounded-lg border border-gray-200 px-3 py-2.5 dark:border-gray-800">
                  <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
                    <p className="text-sm text-gray-900 dark:text-white">{describeEntry(entry)}</p>
                    <time
                      dateTime={entry.occurredAtUtc}
                      className="whitespace-nowrap text-xs text-gray-500 dark:text-gray-400"
                    >
                      {new Date(entry.occurredAtUtc).toLocaleString()}
                    </time>
                  </div>
                  <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">by {describeActor(entry)}</p>

                  <details className="mt-2 text-xs text-gray-600 dark:text-gray-400">
                    <summary className="cursor-pointer select-none font-medium text-gray-700 hover:underline dark:text-gray-300">
                      Audit details
                    </summary>
                    <dl className="mt-2 grid grid-cols-[max-content_1fr] gap-x-3 gap-y-1">
                      <dt className="font-medium">Action</dt>
                      <dd>
                        {entry.action} ({entry.actionCategory})
                      </dd>
                      <dt className="font-medium">Source service</dt>
                      <dd>{entry.sourceService}</dd>
                      <dt className="font-medium">Changed fields</dt>
                      <dd>{parseChangedFields(entry.changedFields).join(', ') || 'None'}</dd>
                      <dt className="font-medium">Trace ID</dt>
                      <dd className="break-all">{entry.traceId}</dd>
                      <dt className="font-medium">Correlation ID</dt>
                      <dd className="break-all">{entry.correlationId}</dd>
                    </dl>
                  </details>
                </li>
              ))}
            </ul>

            {state.totalCount > state.entries.length && (
              <p className="text-xs text-gray-500 dark:text-gray-400">
                Showing {state.entries.length} most recent of {state.totalCount} audit entries.
              </p>
            )}
          </Stack>
        )}
      </Stack>
    </Card>
  );
};
