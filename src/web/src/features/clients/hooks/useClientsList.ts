import { useState, useCallback, useEffect } from 'react';
import { clientsApi } from '@/api/clients';
import type { ClientLifecycleStatus } from '@/api/clients';
import type { ClientListState } from '../types';
import { DEFAULT_PAGE_SIZE } from '../types';

export function useClientsList() {
  const [state, setState] = useState<ClientListState>({
    clients: [],
    isLoading: false,
    error: null,
    pageNumber: 1,
    pageSize: DEFAULT_PAGE_SIZE,
    totalCount: 0,
    totalPages: 0,
    search: '',
    lifecycleStatus: [],
    assignedOwner: '',
    excludeArchived: true,
    sortBy: 'name',
    sortDirection: 'Ascending',
  });

  const fetchClients = useCallback(async () => {
    setState((prev) => ({ ...prev, isLoading: true, error: null }));

    try {
      const result = await clientsApi.listClients({
        pageNumber: state.pageNumber,
        pageSize: state.pageSize,
        search: state.search || undefined,
        lifecycleStatus: state.lifecycleStatus.length > 0 ? state.lifecycleStatus : undefined,
        assignedOwner: state.assignedOwner || undefined,
        excludeArchived: state.excludeArchived,
        sortBy: state.sortBy,
        sortDirection: state.sortDirection,
      });

      if (!result) {
        throw new Error('No response from server. Check if gateway is running and VITE_API_BASE_URL is configured.');
      }

      setState((prev) => ({
        ...prev,
        clients: result.items || [],
        totalCount: result.totalCount || 0,
        totalPages: result.totalPages || 0,
        isLoading: false,
      }));
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to load clients';
      setState((prev) => ({
        ...prev,
        isLoading: false,
        error: message,
        clients: [],
      }));
    }
  }, [state.pageNumber, state.pageSize, state.search, state.lifecycleStatus, state.assignedOwner, state.excludeArchived, state.sortBy, state.sortDirection]);

  useEffect(() => {
    fetchClients();
  }, [fetchClients]);

  const setSearch = useCallback((search: string) => {
    setState((prev) => ({ ...prev, search, pageNumber: 1 }));
  }, []);

  const setLifecycleStatus = useCallback((status: ClientLifecycleStatus[]) => {
    setState((prev) => ({ ...prev, lifecycleStatus: status, pageNumber: 1 }));
  }, []);

  const setAssignedOwner = useCallback((owner: string) => {
    setState((prev) => ({ ...prev, assignedOwner: owner, pageNumber: 1 }));
  }, []);

  const setExcludeArchived = useCallback((exclude: boolean) => {
    setState((prev) => ({ ...prev, excludeArchived: exclude, pageNumber: 1 }));
  }, []);

  const setSortBy = useCallback((sortBy: ClientListState['sortBy']) => {
    setState((prev) => ({ ...prev, sortBy, pageNumber: 1 }));
  }, []);

  const setSortDirection = useCallback((direction: 'Ascending' | 'Descending') => {
    setState((prev) => ({ ...prev, sortDirection: direction }));
  }, []);

  const setPageNumber = useCallback((page: number) => {
    setState((prev) => ({ ...prev, pageNumber: page }));
  }, []);

  const setPageSize = useCallback((size: number) => {
    setState((prev) => ({ ...prev, pageSize: size, pageNumber: 1 }));
  }, []);

  const retry = useCallback(() => {
    fetchClients();
  }, [fetchClients]);

  return {
    ...state,
    setSearch,
    setLifecycleStatus,
    setAssignedOwner,
    setExcludeArchived,
    setSortBy,
    setSortDirection,
    setPageNumber,
    setPageSize,
    retry,
  };
}
