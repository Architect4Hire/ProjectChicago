import { type FC, type FormEvent, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Cluster, Field, Grid, Input, PageHeader, Stack, controlBase, cx } from '@/design-system';
import { useAuth } from '@/auth';
import { useCreateClientForm } from './hooks/useCreateClientForm';
import { ClientDuplicateWarnings } from './components/ClientDuplicateWarnings';

/**
 * Client create form/page (CLIENT-001..004). Supplies Outlet content only; AppLayout (header,
 * sidebar, page shell) is provided by AuthenticatedShell/AppLayout via the route tree.
 */
export const ClientCreatePage: FC = () => {
  const navigate = useNavigate();
  const { currentUser } = useAuth();
  const form = useCreateClientForm(currentUser?.userId ?? '');
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
    if (result && !result.hasDuplicates) {
      navigate(`/clients/${result.id}`);
    }
  };

  const isSubmitting = form.status === 'submitting';
  const showDuplicates = form.status === 'success' && form.duplicates.length > 0 && form.createdClientId;

  if (showDuplicates && form.createdClientId) {
    return (
      <Stack className="gap-6">
        <PageHeader title="New Client" description="The client was created." />
        <ClientDuplicateWarnings duplicates={form.duplicates} />
        <Cluster className="justify-end">
          <Button variant="outline" onClick={() => navigate('/clients')}>
            Back to clients
          </Button>
          <Button onClick={() => navigate(`/clients/${form.createdClientId}`)}>Continue to new client</Button>
        </Cluster>
      </Stack>
    );
  }

  return (
    <Stack className="gap-6">
      <PageHeader title="New Client" description="Add a client to the CRM." />

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
            <Field label="Client name" required error={form.fieldErrors.name}>
              <Input
                name="name"
                value={form.values.name}
                onChange={(e) => form.setField('name', e.target.value)}
                invalid={Boolean(form.fieldErrors.name)}
                maxLength={200}
                autoComplete="organization"
                disabled={isSubmitting}
              />
            </Field>

            <Field
              label="Assigned owner"
              required
              hint="User ID of the team member responsible for this client"
              error={form.fieldErrors.ownerUserId}
            >
              <Input
                name="ownerUserId"
                value={form.values.ownerUserId}
                onChange={(e) => form.setField('ownerUserId', e.target.value)}
                invalid={Boolean(form.fieldErrors.ownerUserId)}
                maxLength={128}
                disabled={isSubmitting}
              />
            </Field>

            <Field label="Primary contact name" error={form.fieldErrors.primaryContactName}>
              <Input
                name="primaryContactName"
                value={form.values.primaryContactName}
                onChange={(e) => form.setField('primaryContactName', e.target.value)}
                invalid={Boolean(form.fieldErrors.primaryContactName)}
                maxLength={200}
                autoComplete="name"
                disabled={isSubmitting}
              />
            </Field>

            <Field label="Primary email" error={form.fieldErrors.primaryEmail}>
              <Input
                type="email"
                name="primaryEmail"
                value={form.values.primaryEmail}
                onChange={(e) => form.setField('primaryEmail', e.target.value)}
                invalid={Boolean(form.fieldErrors.primaryEmail)}
                maxLength={320}
                autoComplete="email"
                disabled={isSubmitting}
              />
            </Field>

            <Field label="Primary phone" error={form.fieldErrors.primaryPhone}>
              <Input
                type="tel"
                name="primaryPhone"
                value={form.values.primaryPhone}
                onChange={(e) => form.setField('primaryPhone', e.target.value)}
                invalid={Boolean(form.fieldErrors.primaryPhone)}
                maxLength={32}
                autoComplete="tel"
                disabled={isSubmitting}
              />
            </Field>

            <Field label="Website" error={form.fieldErrors.website}>
              <Input
                type="url"
                name="website"
                value={form.values.website}
                onChange={(e) => form.setField('website', e.target.value)}
                invalid={Boolean(form.fieldErrors.website)}
                maxLength={2048}
                placeholder="https://"
                autoComplete="url"
                disabled={isSubmitting}
              />
            </Field>

            <Field label="Address" error={form.fieldErrors.addressLine}>
              <Input
                name="addressLine"
                value={form.values.addressLine}
                onChange={(e) => form.setField('addressLine', e.target.value)}
                invalid={Boolean(form.fieldErrors.addressLine)}
                maxLength={300}
                autoComplete="street-address"
                disabled={isSubmitting}
              />
            </Field>

            <Field label="City" error={form.fieldErrors.city}>
              <Input
                name="city"
                value={form.values.city}
                onChange={(e) => form.setField('city', e.target.value)}
                invalid={Boolean(form.fieldErrors.city)}
                maxLength={150}
                autoComplete="address-level2"
                disabled={isSubmitting}
              />
            </Field>

            <Field label="State / Province" error={form.fieldErrors.stateOrProvince}>
              <Input
                name="stateOrProvince"
                value={form.values.stateOrProvince}
                onChange={(e) => form.setField('stateOrProvince', e.target.value)}
                invalid={Boolean(form.fieldErrors.stateOrProvince)}
                maxLength={150}
                autoComplete="address-level1"
                disabled={isSubmitting}
              />
            </Field>

            <Field label="Postal code" error={form.fieldErrors.postalCode}>
              <Input
                name="postalCode"
                value={form.values.postalCode}
                onChange={(e) => form.setField('postalCode', e.target.value)}
                invalid={Boolean(form.fieldErrors.postalCode)}
                maxLength={20}
                autoComplete="postal-code"
                disabled={isSubmitting}
              />
            </Field>

            <Field label="Country" error={form.fieldErrors.country}>
              <Input
                name="country"
                value={form.values.country}
                onChange={(e) => form.setField('country', e.target.value)}
                invalid={Boolean(form.fieldErrors.country)}
                maxLength={100}
                autoComplete="country-name"
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
            <Button type="button" variant="outline" onClick={() => navigate('/clients')} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button type="submit" isLoading={isSubmitting}>
              Create client
            </Button>
          </Cluster>
        </Stack>
      </form>
    </Stack>
  );
};
