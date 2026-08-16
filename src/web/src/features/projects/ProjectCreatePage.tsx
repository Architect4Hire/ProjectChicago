import { type FC, type FormEvent, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Cluster, Field, Grid, Input, PageHeader, Select, Stack, controlBase, cx } from '@/design-system';
import { useAuth } from '@/auth';
import { useCreateProjectForm } from './hooks/useCreateProjectForm';
import { useAvailableClients } from './hooks/useAvailableClients';

/**
 * Project create form/page (PROJECT-001, PROJECT-002). Supplies Outlet content only; AppLayout
 * (header, sidebar, page shell) is provided by AuthenticatedShell/AppLayout via the route tree.
 */
export const ProjectCreatePage: FC = () => {
  const navigate = useNavigate();
  const { currentUser } = useAuth();
  const form = useCreateProjectForm(currentUser?.userId ?? '');
  const { clients, isLoading: clientsLoading, error: clientsError } = useAvailableClients();
  const formRef = useRef<HTMLFormElement>(null);

  // UX-003/frontend.md: move focus to the first invalid field so keyboard/screen-reader users
  // land on the problem instead of a form that silently stayed in place.
  useEffect(() => {
    const firstInvalidField = Object.keys(form.fieldErrors)[0];
    if (!firstInvalidField) {
      return;
    }
    const control = formRef.current?.querySelector<HTMLElement>(`[name="${firstInvalidField}"]`);
    control?.focus();
  }, [form.fieldErrors]);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const result = await form.submit();
    if (result) {
      navigate(`/projects/${result.id}`);
    }
  };

  const isSubmitting = form.status === 'submitting';

  // UX-005: loading state
  if (clientsLoading) {
    return (
      <Stack className="gap-6">
        <PageHeader title="New Project" description="Create a new project for a client." />
        <div className="flex items-center justify-center p-8">
          <div className="text-center">
            <p className="text-sm text-gray-600 dark:text-gray-400">Loading clients...</p>
          </div>
        </div>
      </Stack>
    );
  }

  // UX-005: error state
  if (clientsError) {
    return (
      <Stack className="gap-6">
        <PageHeader title="New Project" description="Create a new project for a client." />
        <div
          role="alert"
          className="rounded-lg border border-error-300 bg-error-50 px-4 py-3 text-sm text-error-700 dark:border-error-800 dark:bg-error-900/20 dark:text-error-400"
        >
          Failed to load clients: {clientsError}
        </div>
        <Cluster className="justify-end">
          <Button type="button" variant="outline" onClick={() => navigate('/projects')}>
            Back to projects
          </Button>
        </Cluster>
      </Stack>
    );
  }

  // UX-005: empty state (no clients available)
  if (clients.length === 0) {
    return (
      <Stack className="gap-6">
        <PageHeader title="New Project" description="Create a new project for a client." />
        <div className="flex items-center justify-center rounded-lg border border-gray-300 p-8 dark:border-gray-700">
          <div className="text-center">
            <p className="text-sm text-gray-700 dark:text-gray-300">No clients available.</p>
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">Create a client first to add a project.</p>
          </div>
        </div>
        <Cluster className="justify-end">
          <Button type="button" variant="outline" onClick={() => navigate('/clients/new')}>
            Create a client
          </Button>
        </Cluster>
      </Stack>
    );
  }

  return (
    <Stack className="gap-6">
      <PageHeader title="New Project" description="Create a new project for a client." />

      <form ref={formRef} onSubmit={handleSubmit} noValidate>
        <Stack className="gap-6">
          {form.formError && (
            <div
              role="alert"
              className="rounded-lg border border-error-300 bg-error-50 px-4 py-3 text-sm text-error-700 dark:border-error-800 dark:bg-error-900/20 dark:text-error-400"
            >
              {form.formError}
            </div>
          )}

          <Grid className="sm:grid-cols-2">
            <Field label="Client" required error={form.fieldErrors.clientId}>
              <Select
                name="clientId"
                value={form.values.clientId}
                onChange={(e) => form.setField('clientId', e.target.value)}
                invalid={Boolean(form.fieldErrors.clientId)}
                disabled={isSubmitting}
              >
                <option value="">Select a client...</option>
                {clients.map((client) => (
                  <option key={client.id} value={client.id}>
                    {client.name}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label="Project name" required error={form.fieldErrors.name}>
              <Input
                name="name"
                value={form.values.name}
                onChange={(e) => form.setField('name', e.target.value)}
                invalid={Boolean(form.fieldErrors.name)}
                maxLength={200}
                disabled={isSubmitting}
              />
            </Field>

            <div className="sm:col-span-2">
              <Field label="Description" error={form.fieldErrors.description}>
                <textarea
                  name="description"
                  className={cx(controlBase, 'min-h-28 px-3.5 py-2.5 text-sm')}
                  value={form.values.description}
                  onChange={(e) => form.setField('description', e.target.value)}
                  aria-invalid={Boolean(form.fieldErrors.description) || undefined}
                  maxLength={2000}
                  disabled={isSubmitting}
                />
              </Field>
            </div>
          </Grid>

          <Cluster className="justify-end">
            <Button type="button" variant="outline" onClick={() => navigate('/projects')} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button type="submit" isLoading={isSubmitting}>
              Create project
            </Button>
          </Cluster>
        </Stack>
      </form>
    </Stack>
  );
};
