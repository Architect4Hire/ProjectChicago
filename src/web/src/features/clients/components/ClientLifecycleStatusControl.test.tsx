import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ClientLifecycleStatusControl } from './ClientLifecycleStatusControl';
import * as clientsApiModule from '@/api/clients';
import type { ClientDetailRecord } from '@/api/clients';
import { ConflictError, ValidationError } from '@/api/http';

vi.mock('@/api/clients');

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

function mockChangeLifecycleStatus(impl: (...args: unknown[]) => unknown) {
  const mockChange = vi.fn(impl);
  vi.spyOn(clientsApiModule, 'clientsApi', 'get').mockReturnValue({
    changeLifecycleStatus: mockChange,
  } as unknown as typeof clientsApiModule.clientsApi);
  return mockChange;
}

describe('ClientLifecycleStatusControl', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('offers every non-Archived status except the client\'s current status, and never Archived', async () => {
    mockChangeLifecycleStatus(() => Promise.resolve(buildClient()));
    render(<ClientLifecycleStatusControl client={buildClient({ lifecycleStatus: 'Active' })} onStatusChanged={vi.fn()} />);

    const select = screen.getByLabelText(/Change lifecycle status/i);
    const optionLabels = within(select)
      .getAllByRole('option')
      .map((option) => option.textContent);

    expect(optionLabels).toEqual(['Lead', 'Prospect', 'On Hold', 'Inactive']);
    expect(optionLabels).not.toContain('Active');
    expect(optionLabels).not.toContain('Archived');
  });

  it('renders an explanatory message instead of a control when the client is already Archived', () => {
    render(
      <ClientLifecycleStatusControl client={buildClient({ lifecycleStatus: 'Archived' })} onStatusChanged={vi.fn()} />,
    );

    expect(screen.getByText(/already archived|is archived/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Change status/i })).not.toBeInTheDocument();
  });

  it('shows the pending state while the request is in flight and disables the controls', async () => {
    let resolveChange: (value: unknown) => void = () => {};
    mockChangeLifecycleStatus(
      () =>
        new Promise((resolve) => {
          resolveChange = resolve;
        }),
    );

    const user = userEvent.setup();
    render(<ClientLifecycleStatusControl client={buildClient({ lifecycleStatus: 'Active' })} onStatusChanged={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: /Change status/i }));
    await user.click(screen.getByRole('button', { name: /Confirm/i }));

    const confirmButton = screen.getByRole('button', { name: /Confirm/i });
    expect(confirmButton).toHaveAttribute('aria-busy', 'true');
    expect(screen.getByLabelText(/Change lifecycle status/i)).toBeDisabled();

    resolveChange(buildClient({ lifecycleStatus: 'Lead' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('shows a rejected-transition error from the server without calling onStatusChanged', async () => {
    const onStatusChanged = vi.fn();
    mockChangeLifecycleStatus(() =>
      Promise.reject(
        new ValidationError(
          { status: 400, title: 'Validation Failed' },
          { newStatus: ['Cannot transition from Active to Active.'] },
        ),
      ),
    );

    const user = userEvent.setup();
    render(<ClientLifecycleStatusControl client={buildClient({ lifecycleStatus: 'Active' })} onStatusChanged={onStatusChanged} />);

    await user.click(screen.getByRole('button', { name: /Change status/i }));
    await user.click(screen.getByRole('button', { name: /Confirm/i }));

    expect(await screen.findByText(/Cannot transition from Active to Active\./i)).toBeInTheDocument();
    expect(onStatusChanged).not.toHaveBeenCalled();
  });

  it('shows a stale-concurrency conflict and only refetches when the user explicitly reloads', async () => {
    const onStatusChanged = vi.fn();
    mockChangeLifecycleStatus(() =>
      Promise.reject(new ConflictError({ status: 409, title: 'Conflict' })),
    );

    const user = userEvent.setup();
    render(<ClientLifecycleStatusControl client={buildClient({ lifecycleStatus: 'Active' })} onStatusChanged={onStatusChanged} />);

    await user.click(screen.getByRole('button', { name: /Change status/i }));
    await user.click(screen.getByRole('button', { name: /Confirm/i }));

    const conflictAlert = await screen.findByRole('alert');
    expect(conflictAlert).toHaveTextContent(/Someone else changed this client/i);
    // The rejected change must not be silently applied - no refetch/callback until the user acts.
    expect(onStatusChanged).not.toHaveBeenCalled();

    await user.click(within(conflictAlert).getByRole('button', { name: /Reload/i }));
    expect(onStatusChanged).toHaveBeenCalledTimes(1);
  });

  it('supports full keyboard operation: open, trap focus, cancel with Escape, then confirm', async () => {
    const onStatusChanged = vi.fn();
    mockChangeLifecycleStatus(() => Promise.resolve(buildClient({ lifecycleStatus: 'Lead' })));

    const user = userEvent.setup();
    render(<ClientLifecycleStatusControl client={buildClient({ lifecycleStatus: 'Active' })} onStatusChanged={onStatusChanged} />);

    // Reach and change the select, then the trigger button, using only the keyboard.
    await user.tab();
    expect(screen.getByLabelText(/Change lifecycle status/i)).toHaveFocus();
    await user.selectOptions(screen.getByLabelText(/Change lifecycle status/i), 'Lead');

    await user.tab();
    const trigger = screen.getByRole('button', { name: /Change status/i });
    expect(trigger).toHaveFocus();
    await user.keyboard('{Enter}');

    // Dialog opens and moves focus inside it (WAI-ARIA dialog pattern).
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('button', { name: /Cancel/i })).toHaveFocus();

    // Escape closes without submitting.
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(onStatusChanged).not.toHaveBeenCalled();

    // Reopen and confirm via keyboard.
    await user.click(trigger);
    const reopenedDialog = await screen.findByRole('dialog');
    await user.keyboard('{Tab}');
    expect(within(reopenedDialog).getByRole('button', { name: /Confirm/i })).toHaveFocus();
    await user.keyboard('{Enter}');

    await waitFor(() => expect(onStatusChanged).toHaveBeenCalledTimes(1));
  });
});
