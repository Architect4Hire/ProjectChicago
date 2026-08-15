import { useCallback, useEffect, useMemo, useState } from 'react';
import { auditApi } from '@/api/audit';
import type { AuditEntry } from '@/api/audit';
import { AuthorizationError, HttpError } from '@/api';
import { useAuth } from '@/auth';

const RECENT_ACTIVITY_PAGE_SIZE = 10;

/**
 * Roles granted the backend's Audit.Read policy (ProjectChicago.Audit Program.cs). Used only as a
 * client-side hint to avoid firing a request that will predictably 403 - the backend policy is
 * still the actual security control (security.md: guards are navigation aids, not enforcement).
 */
const AUDIT_READ_ROLES = ['Administrator', 'Manager'];

export interface ClientActivityState {
  entries: AuditEntry[];
  totalCount: number;
  isLoading: boolean;
  error: string | null;
  /** False when the current user lacks Audit.Read - via role hint or a live 403. */
  isAuthorized: boolean;
}

/**
 * Loads recent activity/audit history for one Client (ACTIVITY-001..003, CLIENT-030). Separate
 * from useClientDetail: this reads through the Audit Service's own API (ADR-0016) and is gated by
 * a distinct authorization policy, so it fails/loads independently of the core Client detail.
 */
export function useClientActivity(clientId: string) {
  const { currentUser } = useAuth();
  const isAuthorizedHint = useMemo(
    () => (currentUser?.roles ?? []).some((role) => AUDIT_READ_ROLES.includes(role)),
    [currentUser],
  );

  const [state, setState] = useState<ClientActivityState>({
    entries: [],
    totalCount: 0,
    isLoading: isAuthorizedHint,
    error: null,
    isAuthorized: isAuthorizedHint,
  });

  const fetchActivity = useCallback(
    async (signal?: AbortSignal) => {
      if (!clientId) {
        setState({ entries: [], totalCount: 0, isLoading: false, error: null, isAuthorized: isAuthorizedHint });
        return;
      }

      if (!isAuthorizedHint) {
        setState({ entries: [], totalCount: 0, isLoading: false, error: null, isAuthorized: false });
        return;
      }

      setState((prev) => ({ ...prev, isLoading: true, error: null }));

      try {
        const result = await auditApi.getEntriesByEntity(
          'Client',
          clientId,
          { pageNumber: 1, pageSize: RECENT_ACTIVITY_PAGE_SIZE },
          { signal },
        );
        if (signal?.aborted) return;
        setState({ entries: result.items, totalCount: result.totalCount, isLoading: false, error: null, isAuthorized: true });
      } catch (err) {
        if (signal?.aborted) return;

        if (err instanceof AuthorizationError) {
          setState({ entries: [], totalCount: 0, isLoading: false, error: null, isAuthorized: false });
          return;
        }

        const message = err instanceof HttpError ? err.problemDetails.detail || err.message : 'Failed to load activity';
        setState((prev) => ({ ...prev, isLoading: false, error: message }));
      }
    },
    [clientId, isAuthorizedHint],
  );

  useEffect(() => {
    const controller = new AbortController();
    fetchActivity(controller.signal);
    return () => controller.abort();
  }, [fetchActivity]);

  const retry = useCallback(() => {
    fetchActivity();
  }, [fetchActivity]);

  return { ...state, retry };
}
