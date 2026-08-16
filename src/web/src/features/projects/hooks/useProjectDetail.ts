import { useState, useCallback, useEffect } from 'react';
import { projectsApi } from '@/api/projects';
import type { ProjectDetailState } from '../types';

export function useProjectDetail(projectId: string) {
  const [state, setState] = useState<ProjectDetailState>({
    detail: null,
    isLoading: false,
    error: null,
    notFound: false,
  });

  const fetchDetail = useCallback(async () => {
    if (!projectId) {
      setState({ detail: null, isLoading: false, error: null, notFound: true });
      return;
    }

    setState((prev) => ({ ...prev, isLoading: true, error: null, notFound: false }));

    try {
      const result = await projectsApi.getProject(projectId);

      if (!result) {
        setState((prev) => ({
          ...prev,
          isLoading: false,
          notFound: true,
          detail: null,
        }));
        return;
      }

      setState({
        detail: result,
        isLoading: false,
        error: null,
        notFound: false,
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to load project';
      // 404 errors typically indicate not found
      const isNotFound = err instanceof Error && message.includes('404');
      setState((prev) => ({
        ...prev,
        isLoading: false,
        error: isNotFound ? null : message,
        notFound: isNotFound,
        detail: null,
      }));
    }
  }, [projectId]);

  useEffect(() => {
    fetchDetail();
  }, [fetchDetail]);

  const retry = useCallback(() => {
    fetchDetail();
  }, [fetchDetail]);

  const refetch = useCallback(() => {
    fetchDetail();
  }, [fetchDetail]);

  return {
    ...state,
    retry,
    refetch,
  };
}
