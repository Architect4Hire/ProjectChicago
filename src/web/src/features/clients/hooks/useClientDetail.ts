import { useCallback, useEffect, useState } from 'react';
import { clientsApi } from '@/api/clients';
import type { ClientDetail } from '@/api/clients';
import { HttpError, NotFoundError } from '@/api';

export interface ClientDetailState {
  detail: ClientDetail | null;
  isLoading: boolean;
  error: string | null;
  notFound: boolean;
}

/**
 * Loads the consolidated Client detail view (CLIENT-030..032) for one Client ID.
 * Distinguishes "not found" (404) from other load failures so the page can show the correct
 * empty-state vs error-state (frontend.md: loading/empty/error/success are first-class).
 */
export function useClientDetail(clientId: string) {
  const [state, setState] = useState<ClientDetailState>({
    detail: null,
    isLoading: true,
    error: null,
    notFound: false,
  });

  const fetchDetail = useCallback(
    async (signal?: AbortSignal) => {
      if (!clientId) {
        setState({ detail: null, isLoading: false, error: null, notFound: false });
        return;
      }

      setState((prev) => ({ ...prev, isLoading: true, error: null, notFound: false }));

      try {
        const detail = await clientsApi.getClient(clientId, { signal });
        if (signal?.aborted) return;
        setState({ detail, isLoading: false, error: null, notFound: false });
      } catch (err) {
        if (signal?.aborted) return;

        if (err instanceof NotFoundError) {
          setState({ detail: null, isLoading: false, error: null, notFound: true });
          return;
        }

        const message = err instanceof HttpError ? err.problemDetails.detail || err.message : 'Failed to load client';
        setState({ detail: null, isLoading: false, error: message, notFound: false });
      }
    },
    [clientId],
  );

  useEffect(() => {
    const controller = new AbortController();
    fetchDetail(controller.signal);
    return () => controller.abort();
  }, [fetchDetail]);

  const retry = useCallback(() => {
    fetchDetail();
  }, [fetchDetail]);

  return { ...state, retry };
}
