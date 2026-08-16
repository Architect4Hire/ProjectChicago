import { type FC } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, EmptyState, ErrorState, PageHeader, Spinner, Stack } from '@/design-system';
import { useProjectDetail } from './hooks/useProjectDetail';
import { ProjectOverviewCard } from './components/ProjectOverviewCard';
import { ProjectTasksSection } from './components/ProjectTasksSection';

/**
 * Project detail page (PROJECT-030..031).
 * Supplies Outlet content only; AppLayout (header, sidebar, page shell) is provided by
 * AuthenticatedShell/AppLayout via the route tree.
 */
export const ProjectDetailPage: FC = () => {
  const { projectId } = useParams<{ projectId: string }>();
  const navigate = useNavigate();
  const detailState = useProjectDetail(projectId ?? '');

  if (!projectId) {
    return (
      <EmptyState
        title="Project not found"
        description="No project identifier was provided."
        action={
          <Button variant="outline" onClick={() => navigate('/projects')}>
            Back to projects
          </Button>
        }
      />
    );
  }

  if (detailState.isLoading) {
    return (
      <div className="flex min-h-96 items-center justify-center">
        <Spinner label="Loading project..." />
      </div>
    );
  }

  if (detailState.notFound) {
    return (
      <Stack className="gap-6">
        <PageHeader title="Project not found" />
        <EmptyState
          title="Project not found"
          description="This project may have been archived, removed, or the link is incorrect."
          action={
            <Button variant="outline" onClick={() => navigate('/projects')}>
              Back to projects
            </Button>
          }
        />
      </Stack>
    );
  }

  if (detailState.error || !detailState.detail) {
    return (
      <Stack className="gap-6">
        <PageHeader title="Project detail" />
        <ErrorState retry={detailState.retry} />
      </Stack>
    );
  }

  const { project, openTasks, completedTasks } = detailState.detail;

  return (
    <Stack className="gap-6">
      <PageHeader
        title={project.name}
        description="Project information, status, related tasks, and recent activity."
        actions={
          <Button variant="outline" onClick={() => navigate('/projects')}>
            Back to projects
          </Button>
        }
      />

      <ProjectOverviewCard
        project={project}
        openTasks={openTasks}
        onProjectChanged={detailState.refetch}
      />

      <ProjectTasksSection openTasks={openTasks} completedTasks={completedTasks} />
    </Stack>
  );
};
