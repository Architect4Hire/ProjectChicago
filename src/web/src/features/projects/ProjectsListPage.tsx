import { type FC, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Stack, PageHeader, Spinner, EmptyState, ErrorState, Button, Cluster } from '@/design-system';
import { useProjectsList } from './hooks/useProjectsList';
import { ProjectsFilter } from './components/ProjectsFilter';
import { ProjectsTable } from './components/ProjectsTable';
import { ProjectsPagination } from './components/ProjectsPagination';

/**
 * Projects list page (PROJECT-020..023).
 * Supplies Outlet content only; AppLayout (header, sidebar, page shell) is provided by
 * AuthenticatedShell/AppLayout via the route tree.
 */
export const ProjectsListPage: FC = () => {
  const navigate = useNavigate();
  const [showFilters, setShowFilters] = useState(false);
  const listState = useProjectsList();

  return (
    <Stack className="gap-6">
      <PageHeader
        title="Projects"
        description="Manage and organize your projects"
        actions={
          <Cluster>
            <Button onClick={() => navigate('/projects/new')}>
              Create Project
            </Button>
            <Button
              variant="outline"
              onClick={() => setShowFilters(!showFilters)}
              aria-label={showFilters ? 'Hide filters' : 'Show filters'}
            >
              {showFilters ? 'Hide' : 'Show'} Filters
            </Button>
          </Cluster>
        }
      />

      {showFilters && (
        <ProjectsFilter
          search={listState.search}
          onSearchChange={listState.setSearch}
          clientId={listState.clientId}
          onClientIdChange={listState.setClientId}
          status={listState.status}
          onStatusChange={listState.setStatus}
          ownerUserId={listState.ownerUserId}
          onOwnerUserIdChange={listState.setOwnerUserId}
          priority={listState.priority}
          onPriorityChange={listState.setPriority}
          startDateFromUtc={listState.startDateFromUtc}
          startDateToUtc={listState.startDateToUtc}
          onStartDateRangeChange={listState.setStartDateRange}
          targetCompletionDateFromUtc={listState.targetCompletionDateFromUtc}
          targetCompletionDateToUtc={listState.targetCompletionDateToUtc}
          onTargetCompletionDateRangeChange={listState.setTargetCompletionDateRange}
          excludeArchived={listState.excludeArchived}
          onExcludeArchivedChange={listState.setExcludeArchived}
        />
      )}

      {listState.isLoading && (
        <div className="flex min-h-96 items-center justify-center">
          <Spinner label="Loading projects..." />
        </div>
      )}

      {!listState.isLoading && listState.error && (
        <ErrorState retry={listState.retry} />
      )}

      {!listState.isLoading && !listState.error && listState.projects.length === 0 && (
        <EmptyState
          title="No projects found"
          description={
            listState.search ||
            listState.clientId ||
            listState.status.length > 0 ||
            listState.ownerUserId ||
            listState.priority.length > 0 ||
            !listState.excludeArchived
              ? 'Try adjusting your search or filter criteria'
              : 'Start by creating your first project'
          }
        />
      )}

      {!listState.isLoading && !listState.error && listState.projects.length > 0 && (
        <>
          <div className="rounded-lg border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-950">
            <ProjectsTable
              projects={listState.projects}
              sortBy={listState.sortBy}
              sortDirection={listState.sortDirection}
              onSortChange={(sortBy, direction) => {
                listState.setSortBy(sortBy as any);
                listState.setSortDirection(direction === 'asc' ? 'Ascending' : 'Descending');
              }}
            />
          </div>

          {listState.totalPages > 1 && (
            <ProjectsPagination
              pageNumber={listState.pageNumber}
              pageSize={listState.pageSize}
              totalPages={listState.totalPages}
              totalCount={listState.totalCount}
              onPageChange={listState.setPageNumber}
              onPageSizeChange={listState.setPageSize}
            />
          )}
        </>
      )}
    </Stack>
  );
};
