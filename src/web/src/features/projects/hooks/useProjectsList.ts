import { useState, useCallback, useEffect } from 'react';
import { projectsApi } from '@/api/projects';
import type { ProjectStatus, ProjectPriority } from '@/api/projects';
import type { ProjectListState, ProjectsSortBy } from '../types';
import { DEFAULT_PAGE_SIZE } from '../types';

export function useProjectsList() {
  const [state, setState] = useState<ProjectListState>({
    projects: [],
    isLoading: false,
    error: null,
    pageNumber: 1,
    pageSize: DEFAULT_PAGE_SIZE,
    totalCount: 0,
    totalPages: 0,
    search: '',
    clientId: '',
    status: [],
    ownerUserId: '',
    priority: [],
    startDateFromUtc: '',
    startDateToUtc: '',
    targetCompletionDateFromUtc: '',
    targetCompletionDateToUtc: '',
    excludeArchived: true,
    sortBy: 'name',
    sortDirection: 'Ascending',
  });

  const fetchProjects = useCallback(async () => {
    setState((prev) => ({ ...prev, isLoading: true, error: null }));

    try {
      const result = await projectsApi.listProjects({
        pageNumber: state.pageNumber,
        pageSize: state.pageSize,
        search: state.search || undefined,
        clientId: state.clientId || undefined,
        status: state.status.length > 0 ? state.status : undefined,
        ownerUserId: state.ownerUserId || undefined,
        priority: state.priority.length > 0 ? state.priority : undefined,
        startDateFromUtc: state.startDateFromUtc || undefined,
        startDateToUtc: state.startDateToUtc || undefined,
        targetCompletionDateFromUtc: state.targetCompletionDateFromUtc || undefined,
        targetCompletionDateToUtc: state.targetCompletionDateToUtc || undefined,
        excludeArchived: state.excludeArchived,
        sortBy: state.sortBy,
        sortDirection: state.sortDirection,
      });

      if (!result) {
        throw new Error('No response from server. Check if gateway is running and VITE_API_BASE_URL is configured.');
      }

      setState((prev) => ({
        ...prev,
        projects: result.items || [],
        totalCount: result.totalCount || 0,
        totalPages: result.totalPages || 0,
        isLoading: false,
      }));
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to load projects';
      setState((prev) => ({
        ...prev,
        isLoading: false,
        error: message,
        projects: [],
      }));
    }
  }, [
    state.pageNumber,
    state.pageSize,
    state.search,
    state.clientId,
    state.status,
    state.ownerUserId,
    state.priority,
    state.startDateFromUtc,
    state.startDateToUtc,
    state.targetCompletionDateFromUtc,
    state.targetCompletionDateToUtc,
    state.excludeArchived,
    state.sortBy,
    state.sortDirection,
  ]);

  useEffect(() => {
    fetchProjects();
  }, [fetchProjects]);

  const setSearch = useCallback((search: string) => {
    setState((prev) => ({ ...prev, search, pageNumber: 1 }));
  }, []);

  const setClientId = useCallback((clientId: string) => {
    setState((prev) => ({ ...prev, clientId, pageNumber: 1 }));
  }, []);

  const setStatus = useCallback((status: ProjectStatus[]) => {
    setState((prev) => ({ ...prev, status, pageNumber: 1 }));
  }, []);

  const setOwnerUserId = useCallback((ownerUserId: string) => {
    setState((prev) => ({ ...prev, ownerUserId, pageNumber: 1 }));
  }, []);

  const setPriority = useCallback((priority: ProjectPriority[]) => {
    setState((prev) => ({ ...prev, priority, pageNumber: 1 }));
  }, []);

  const setStartDateRange = useCallback((fromUtc: string, toUtc: string) => {
    setState((prev) => ({ ...prev, startDateFromUtc: fromUtc, startDateToUtc: toUtc, pageNumber: 1 }));
  }, []);

  const setTargetCompletionDateRange = useCallback((fromUtc: string, toUtc: string) => {
    setState((prev) => ({ ...prev, targetCompletionDateFromUtc: fromUtc, targetCompletionDateToUtc: toUtc, pageNumber: 1 }));
  }, []);

  const setExcludeArchived = useCallback((exclude: boolean) => {
    setState((prev) => ({ ...prev, excludeArchived: exclude, pageNumber: 1 }));
  }, []);

  const setSortBy = useCallback((sortBy: ProjectsSortBy) => {
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
    fetchProjects();
  }, [fetchProjects]);

  return {
    ...state,
    setSearch,
    setClientId,
    setStatus,
    setOwnerUserId,
    setPriority,
    setStartDateRange,
    setTargetCompletionDateRange,
    setExcludeArchived,
    setSortBy,
    setSortDirection,
    setPageNumber,
    setPageSize,
    retry,
  };
}
