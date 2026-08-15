import { type FC, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Cluster, Stack, PageHeader, Spinner, EmptyState, ErrorState } from '@/design-system';
import { useClientsList } from './hooks/useClientsList';
import { ClientsFilter } from './components/ClientsFilter';
import { ClientsTable } from './components/ClientsTable';
import { ClientsPagination } from './components/ClientsPagination';

export const ClientsListPage: FC = () => {
  const navigate = useNavigate();
  const [showFilters, setShowFilters] = useState(false);
  const listState = useClientsList();

  return (
    <Stack className="gap-6">
      <PageHeader
        title="Clients"
        description="Manage and organize your clients"
        actions={
          <Cluster>
            <button
              onClick={() => setShowFilters(!showFilters)}
              className="rounded-md border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-900"
              aria-label={showFilters ? 'Hide filters' : 'Show filters'}
            >
              {showFilters ? 'Hide' : 'Show'} Filters
            </button>
            <Button onClick={() => navigate('/clients/new')}>New Client</Button>
          </Cluster>
        }
      />

      {showFilters && (
        <ClientsFilter
          search={listState.search}
          onSearchChange={listState.setSearch}
          lifecycleStatus={listState.lifecycleStatus}
          onLifecycleStatusChange={listState.setLifecycleStatus}
          assignedOwner={listState.assignedOwner}
          onAssignedOwnerChange={listState.setAssignedOwner}
          excludeArchived={listState.excludeArchived}
          onExcludeArchivedChange={listState.setExcludeArchived}
        />
      )}

      {listState.isLoading && (
        <div className="flex min-h-96 items-center justify-center">
          <Spinner label="Loading clients..." />
        </div>
      )}

      {!listState.isLoading && listState.error && (
        <ErrorState retry={listState.retry} />
      )}

      {!listState.isLoading && !listState.error && listState.clients.length === 0 && (
        <EmptyState
          title="No clients found"
          description={
            listState.search ||
            listState.lifecycleStatus.length > 0 ||
            listState.assignedOwner ||
            !listState.excludeArchived
              ? 'Try adjusting your search or filter criteria'
              : 'Start by creating your first client'
          }
        />
      )}

      {!listState.isLoading && !listState.error && listState.clients.length > 0 && (
        <>
          <div className="rounded-lg border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-950">
            <ClientsTable
              clients={listState.clients}
              sortBy={listState.sortBy}
              sortDirection={listState.sortDirection}
              onSortChange={(sortBy, direction) => {
                listState.setSortBy(sortBy);
                listState.setSortDirection(direction === 'asc' ? 'Ascending' : 'Descending');
              }}
            />
          </div>

          {listState.totalPages > 1 && (
            <ClientsPagination
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
