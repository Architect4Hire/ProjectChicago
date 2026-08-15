import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ClientCreatePage } from './ClientCreatePage';
import * as clientsApiModule from '@/api/clients';
import type { Client } from '@/api/clients';
import { ValidationError, HttpError } from '@/api/http';
import * as useAuthModule from '@/auth';

vi.mock('@/api/clients');
vi.mock('@/auth', () => ({ useAuth: vi.fn() }));

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => mockNavigate };
});

function buildClient(overrides: Partial<Client> = {}): Client {
  return {
    id: 'client-1',
    name: 'Acme Corp',
    primaryContactName: '',
    primaryEmail: '',
    primaryPhone: '',
    website: '',
    address: '',
    city: '',
    state: '',
    postalCode: '',
    country: '',
    lifecycleStatus: 'Lead',
    description: '',
    assignedOwner: 'owner-1',
    createdDate: '2026-08-15T00:00:00Z',
    createdBy: 'owner-1',
    lastModifiedDate: '2026-08-15T00:00:00Z',
    lastModifiedBy: 'owner-1',
    possibleDuplicates: [],
    ...overrides,
  };
}

function mockCreateClientReturning(impl: (...args: unknown[]) => unknown) {
  const mockCreateClient = vi.fn(impl);
  vi.spyOn(clientsApiModule, 'clientsApi', 'get').mockReturnValue({
    createClient: mockCreateClient,
  } as unknown as typeof clientsApiModule.clientsApi);
  return mockCreateClient;
}

function mockAuthenticatedOwner(userId: string | undefined) {
  (useAuthModule.useAuth as any).mockReturnValue({
    currentUser: userId ? { userId, email: 'owner@example.com', userName: 'owner', roles: [] } : null,
    isLoading: false,
    isAuthenticated: true,
    error: null,
    login: vi.fn(),
    logout: vi.fn(),
    refreshUser: vi.fn(),
  });
}

async function fillRequiredFields(name = 'Acme Corp') {
  await userEvent.type(screen.getByLabelText(/Client name/i), name);
}

describe('ClientCreatePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAuthenticatedOwner('owner-1');
  });

  it('displays required-field validation errors and does not submit when name/owner are blank', async () => {
    mockAuthenticatedOwner(undefined);
    const mockCreateClient = mockCreateClientReturning(() => Promise.resolve(buildClient()));

    render(<ClientCreatePage />);

    await userEvent.click(screen.getByRole('button', { name: /Create client/i }));

    expect(await screen.findByText('Client name is required.')).toBeInTheDocument();
    expect(screen.getByText('Assigned owner is required.')).toBeInTheDocument();
    expect(mockCreateClient).not.toHaveBeenCalled();
  });

  it('shows a pending/submitting state while the create request is in flight', async () => {
    const mockCreateClient = mockCreateClientReturning(() => new Promise(() => {}));

    render(<ClientCreatePage />);
    await fillRequiredFields();

    const submitButton = screen.getByRole('button', { name: /Create client/i });
    await userEvent.click(submitButton);

    await waitFor(() => {
      expect(submitButton).toHaveAttribute('aria-busy', 'true');
    });
    expect(screen.getByRole('button', { name: /Cancel/i })).toBeDisabled();
    expect(mockCreateClient).toHaveBeenCalledTimes(1);
  });

  it('maps a server-side field validation error onto the relevant field', async () => {
    mockCreateClientReturning(() =>
      Promise.reject(
        new ValidationError({ status: 400 }, { PrimaryEmail: ['The PrimaryEmail field is not a valid e-mail address.'] }),
      ),
    );

    render(<ClientCreatePage />);
    await fillRequiredFields();
    await userEvent.click(screen.getByRole('button', { name: /Create client/i }));

    expect(
      await screen.findByText('The PrimaryEmail field is not a valid e-mail address.'),
    ).toBeInTheDocument();
  });

  it('displays a generic server error banner when the request fails without field errors', async () => {
    mockCreateClientReturning(() => Promise.reject(new HttpError(500, {}, 'HTTP 500')));

    render(<ClientCreatePage />);
    await fillRequiredFields();
    await userEvent.click(screen.getByRole('button', { name: /Create client/i }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('The client could not be saved. Try again.');
  });

  it('renders duplicate warnings after a successful save and defers navigation until acknowledged', async () => {
    mockCreateClientReturning(() =>
      Promise.resolve(
        buildClient({
          id: 'new-client-id',
          possibleDuplicates: [
            { clientId: 'existing-client-id', name: 'Acme Corp', matchedOn: ['Name', 'PrimaryEmail'] },
          ],
        }),
      ),
    );

    render(<ClientCreatePage />);
    await fillRequiredFields();
    await userEvent.click(screen.getByRole('button', { name: /Create client/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Possible duplicate client found');
    expect(screen.getByText(/matched on Name, Email/)).toBeInTheDocument();
    expect(mockNavigate).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: /Continue to new client/i }));
    expect(mockNavigate).toHaveBeenCalledWith('/clients/new-client-id');
  });

  it('navigates to the client detail route on a successful save with no duplicates', async () => {
    mockCreateClientReturning(() => Promise.resolve(buildClient({ id: 'new-client-id', possibleDuplicates: [] })));

    render(<ClientCreatePage />);
    await fillRequiredFields();
    await userEvent.click(screen.getByRole('button', { name: /Create client/i }));

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/clients/new-client-id');
    });
  });
});
