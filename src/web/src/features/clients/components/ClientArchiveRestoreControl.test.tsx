import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ClientArchiveRestoreControl } from './ClientArchiveRestoreControl';
import * as clientsApiModule from '@/api/clients';
import type { ClientDetailRecord } from '@/api/clients';
import { ConflictError, ValidationError } from '@/api/http';
import * as useAuthModule from '@/auth';

vi.mock('@/api/clients');
vi.mock('@/auth', () => ({ useAuth: vi.fn() }));

function buildClient(overrides: Partial<ClientDetailRecord> = {}): ClientDetailRecord {
  return {
    id: 'client-1',
    name: 'Acme Corp',
    primaryContactName: 'John Doe',
    primaryEmail: 'john@acme.com',
    primaryPhone: '555-0100',
    website: 'https://acme.com',
    addressLine: '123 Main St',
    city: 'Springfield',
    stateOrProvince: 'IL',
    postalCode: '62701',
    country: 'USA',
    lifecycleStatus: 'Active',
    description: 'Leading manufacturing company',
    ownerUserId: 'owner-1',
    createdAtUtc: '2026-01-01T00:00:00Z',
    createdBy: 'admin',
    lastModifiedAtUtc: '2026-08-01T00:00:00Z',
    lastModifiedBy: 'owner-1',
    concurrencyToken: 'AAAAAAAAB9E=',
    ...overrides,
  };
}

function mockClientsApi(overrides: Partial<typeof clientsApiModule.clientsApi> = {}) {
  const merged = {
    archiveClient: vi.fn(),
    restoreClient: vi.fn(),
    ...overrides,
  };
  vi.spyOn(clientsApiModule, 'clientsApi', 'get').mockReturnValue(
    merged as unknown as typeof clientsApiModule.clientsApi,
  );
  return merged;
}

function mockAuthenticatedUser(roles: string[]) {
  (useAuthModule.useAuth as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
    currentUser: { userId: 'owner-1', email: 'owner@example.com', userName: 'owner', roles },
    isLoading: false,
    isAuthenticated: true,
    error: null,
    login: vi.fn(),
    logout: vi.fn(),
    refreshUser: vi.fn(),
  });
}

describe('ClientArchiveRestoreControl', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAuthenticatedUser(['Manager']);
  });

  describe('archiving a non-Archived client', () => {
    it('confirm: archives the client and notifies the parent on success', async () => {
      const { archiveClient } = mockClientsApi({ archiveClient: vi.fn().mockResolvedValue(buildClient({ lifecycleStatus: 'Archived' })) } as any);
      const onChanged = vi.fn();
      const user = userEvent.setup();

      render(<ClientArchiveRestoreControl client={buildClient()} hasActiveProjects={false} onChanged={onChanged} />);

      await user.click(screen.getByRole('button', { name: /Archive client/i }));
      const dialog = await screen.findByRole('dialog');
      expect(dialog).toHaveTextContent(/Archive this client\?/i);

      await user.click(within(dialog).getByRole('button', { name: /^Archive$/i }));

      expect(archiveClient).toHaveBeenCalledWith('client-1', { expectedConcurrencyToken: 'AAAAAAAAB9E=' });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('cancel: closes the dialog without archiving or notifying the parent', async () => {
      const { archiveClient } = mockClientsApi();
      const onChanged = vi.fn();
      const user = userEvent.setup();

      render(<ClientArchiveRestoreControl client={buildClient()} hasActiveProjects={false} onChanged={onChanged} />);

      await user.click(screen.getByRole('button', { name: /Archive client/i }));
      const dialog = await screen.findByRole('dialog');
      await user.click(within(dialog).getByRole('button', { name: /Cancel/i }));

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      expect(archiveClient).not.toHaveBeenCalled();
      expect(onChanged).not.toHaveBeenCalled();
    });

    it('blocked: shows an active-Project blocking message with no confirm action, and never calls archiveClient', async () => {
      const { archiveClient } = mockClientsApi();
      const user = userEvent.setup();

      render(<ClientArchiveRestoreControl client={buildClient()} hasActiveProjects onChanged={vi.fn()} />);

      await user.click(screen.getByRole('button', { name: /Archive client/i }));
      const dialog = await screen.findByRole('dialog');

      expect(dialog).toHaveTextContent(/cannot be archived/i);
      expect(dialog).toHaveTextContent(/active Projects/i);
      expect(within(dialog).queryByRole('button', { name: /^Archive$/i })).not.toBeInTheDocument();
      expect(within(dialog).getByRole('button', { name: /Close/i })).toBeInTheDocument();
      expect(archiveClient).not.toHaveBeenCalled();
    });

    it('blocked: falls back to the server-reported active-Projects conflict when the block only surfaces at submit time', async () => {
      const archiveClient = vi.fn().mockRejectedValue(
        new ConflictError({
          status: 409,
          title: 'Conflict',
          detail: 'A Client containing active Projects cannot be archived (CLIENT-015).',
        }),
      );
      mockClientsApi({ archiveClient } as any);
      const onChanged = vi.fn();
      const user = userEvent.setup();

      render(<ClientArchiveRestoreControl client={buildClient()} hasActiveProjects={false} onChanged={onChanged} />);

      await user.click(screen.getByRole('button', { name: /Archive client/i }));
      let dialog = await screen.findByRole('dialog');
      await user.click(within(dialog).getByRole('button', { name: /^Archive$/i }));

      dialog = await screen.findByRole('dialog');
      expect(dialog).toHaveTextContent(/cannot be archived/i);
      expect(within(dialog).queryByRole('button', { name: /^Archive$/i })).not.toBeInTheDocument();
      expect(onChanged).not.toHaveBeenCalled();
    });

    it('shows a validation error from the server without calling onChanged', async () => {
      mockClientsApi({
        archiveClient: vi.fn().mockRejectedValue(
          new ValidationError({ status: 400, title: 'Validation Failed', detail: 'This client cannot be archived.' }, {}),
        ),
      } as any);
      const onChanged = vi.fn();
      const user = userEvent.setup();

      render(<ClientArchiveRestoreControl client={buildClient()} hasActiveProjects={false} onChanged={onChanged} />);

      await user.click(screen.getByRole('button', { name: /Archive client/i }));
      const dialog = await screen.findByRole('dialog');
      await user.click(within(dialog).getByRole('button', { name: /^Archive$/i }));

      expect(await screen.findByText(/This client cannot be archived\./i)).toBeInTheDocument();
      expect(onChanged).not.toHaveBeenCalled();
    });
  });

  describe('restoring an Archived client', () => {
    it('authorization: hides the restore control and explains why for a user without Administrator/Manager role', () => {
      mockAuthenticatedUser(['Contributor']);
      mockClientsApi();

      render(
        <ClientArchiveRestoreControl
          client={buildClient({ lifecycleStatus: 'Archived' })}
          hasActiveProjects={false}
          onChanged={vi.fn()}
        />,
      );

      expect(screen.getByText(/requires Administrator or Manager access/i)).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /Restore client/i })).not.toBeInTheDocument();
    });

    it('confirm: restores the client to the chosen status and notifies the parent on success', async () => {
      const { restoreClient } = mockClientsApi({
        restoreClient: vi.fn().mockResolvedValue(buildClient({ lifecycleStatus: 'Active' })),
      } as any);
      const onChanged = vi.fn();
      const user = userEvent.setup();

      render(
        <ClientArchiveRestoreControl
          client={buildClient({ lifecycleStatus: 'Archived' })}
          hasActiveProjects={false}
          onChanged={onChanged}
        />,
      );

      await user.selectOptions(screen.getByLabelText(/Restore to status/i), 'Prospect');
      await user.click(screen.getByRole('button', { name: /Restore client/i }));

      const dialog = await screen.findByRole('dialog');
      expect(dialog).toHaveTextContent(/Restore Acme Corp from Archived to Prospect\?/i);
      await user.click(within(dialog).getByRole('button', { name: /Confirm/i }));

      expect(restoreClient).toHaveBeenCalledWith('client-1', {
        restoredStatus: 'Prospect',
        expectedConcurrencyToken: 'AAAAAAAAB9E=',
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it('cancel: closes the dialog without restoring or notifying the parent', async () => {
      const { restoreClient } = mockClientsApi();
      const onChanged = vi.fn();
      const user = userEvent.setup();

      render(
        <ClientArchiveRestoreControl
          client={buildClient({ lifecycleStatus: 'Archived' })}
          hasActiveProjects={false}
          onChanged={onChanged}
        />,
      );

      await user.click(screen.getByRole('button', { name: /Restore client/i }));
      const dialog = await screen.findByRole('dialog');
      await user.click(within(dialog).getByRole('button', { name: /Cancel/i }));

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      expect(restoreClient).not.toHaveBeenCalled();
      expect(onChanged).not.toHaveBeenCalled();
    });

    it('shows a stale-concurrency conflict and only refetches when the user explicitly reloads', async () => {
      mockClientsApi({
        restoreClient: vi.fn().mockRejectedValue(new ConflictError({ status: 409, title: 'Conflict' })),
      } as any);
      const onChanged = vi.fn();
      const user = userEvent.setup();

      render(
        <ClientArchiveRestoreControl
          client={buildClient({ lifecycleStatus: 'Archived' })}
          hasActiveProjects={false}
          onChanged={onChanged}
        />,
      );

      await user.click(screen.getByRole('button', { name: /Restore client/i }));
      const dialog = await screen.findByRole('dialog');
      await user.click(within(dialog).getByRole('button', { name: /Confirm/i }));

      const conflictAlert = await screen.findByRole('alert');
      expect(conflictAlert).toHaveTextContent(/Someone else changed this client/i);
      expect(onChanged).not.toHaveBeenCalled();

      await user.click(within(conflictAlert).getByRole('button', { name: /Reload/i }));
      expect(onChanged).toHaveBeenCalledTimes(1);
    });
  });
});
