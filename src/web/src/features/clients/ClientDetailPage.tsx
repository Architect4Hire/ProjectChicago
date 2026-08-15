import { type FC } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, EmptyState, ErrorState, Grid, PageHeader, Spinner, Stack } from '@/design-system';
import { useClientDetail } from './hooks/useClientDetail';
import { useClientActivity } from './hooks/useClientActivity';
import { ClientOverviewCard } from './components/ClientOverviewCard';
import { ClientLifecycleStatusControl } from './components/ClientLifecycleStatusControl';
import { ClientProjectsSection } from './components/ClientProjectsSection';
import { ClientTasksSection } from './components/ClientTasksSection';
import { ClientActivityPanel } from './components/ClientActivityPanel';

/**
 * Client detail page (CLIENT-030..032, CLIENT-010..015, ACTIVITY-001..003). Supplies Outlet
 * content only; AppLayout (header, sidebar, page shell) is provided by AuthenticatedShell/
 * AppLayout via the route tree. Archive/restore remain a separate, later feature.
 */
export const ClientDetailPage: FC = () => {
  const { clientId } = useParams<{ clientId: string }>();
  const navigate = useNavigate();
  const detailState = useClientDetail(clientId ?? '');
  const activityState = useClientActivity(clientId ?? '');

  if (!clientId) {
    return (
      <EmptyState
        title="Client not found"
        description="No client identifier was provided."
        action={
          <Button variant="outline" onClick={() => navigate('/clients')}>
            Back to clients
          </Button>
        }
      />
    );
  }

  if (detailState.isLoading) {
    return (
      <div className="flex min-h-96 items-center justify-center">
        <Spinner label="Loading client..." />
      </div>
    );
  }

  if (detailState.notFound) {
    return (
      <Stack className="gap-6">
        <PageHeader title="Client not found" />
        <EmptyState
          title="Client not found"
          description="This client may have been archived, removed, or the link is incorrect."
          action={
            <Button variant="outline" onClick={() => navigate('/clients')}>
              Back to clients
            </Button>
          }
        />
      </Stack>
    );
  }

  if (detailState.error || !detailState.detail) {
    return (
      <Stack className="gap-6">
        <PageHeader title="Client detail" />
        <ErrorState retry={detailState.retry} />
      </Stack>
    );
  }

  const { client, activeProjects, historicalProjects, openTasks, recentlyCompletedTasks } = detailState.detail;

  return (
    <Stack className="gap-6">
      <PageHeader
        title={client.name}
        description="Client information, lifecycle, related projects, tasks, and recent activity."
        actions={
          <Button variant="outline" onClick={() => navigate('/clients')}>
            Back to clients
          </Button>
        }
      />

      <ClientOverviewCard
        client={client}
        lifecycleControl={<ClientLifecycleStatusControl client={client} onStatusChanged={detailState.retry} />}
      />

      <Grid className="lg:grid-cols-2">
        <ClientProjectsSection
          clientId={client.id}
          activeProjects={activeProjects}
          historicalProjects={historicalProjects}
        />
        <ClientTasksSection
          clientId={client.id}
          openTasks={openTasks}
          recentlyCompletedTasks={recentlyCompletedTasks}
        />
      </Grid>

      <ClientActivityPanel state={activityState} onRetry={activityState.retry} />
    </Stack>
  );
};
