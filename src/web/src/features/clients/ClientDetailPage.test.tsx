import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ClientDetailPage } from './ClientDetailPage';
import * as clientsApiModule from '@/api/clients';
import type { ClientDetail } from '@/api/clients';
import { HttpError, NotFoundError, AuthorizationError } from '@/api/http';
import * as auditApiModule from '@/api/audit';
import type { AuditEntry } from '@/api/audit';
import * as useAuthModule from '@/auth';

vi.mock('@/api/clients');
vi.mock('@/api/audit');
vi.mock('@/auth', () => ({ useAuth: vi.fn() }));

const mockNavigate = vi.fn();
let mockClientId: string | undefined = 'client-1';

vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    useParams: () => ({ clientId: mockClientId }),
  };
});

function buildClientDetail(overrides: Partial<ClientDetail> = {}): ClientDetail {
  return {
    client: {
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
    },
    activeProjects: [
      {
        id: 'project-1',
        name: 'Website Redesign',
        status: 'Planned',
        priority: 'High',
        ownerUserId: 'owner-1',
        startDateUtc: '2026-01-15T00:00:00Z',
        targetCompletionDateUtc: null,
        actualCompletionDateUtc: null,
        lastModifiedAtUtc: '2026-08-01T00:00:00Z',
      },
    ],
    historicalProjects: [],
    openTasks: [
      {
        id: 'task-1',
        projectId: 'project-1',
        title: 'Draft wireframes',
        status: 'InProgress',
        priority: 'Normal',
        assignedUserId: 'owner-1',
        dueDateUtc: '2026-09-01T00:00:00Z',
        completedAtUtc: null,
      },
    ],
    recentlyCompletedTasks: [],
    ...overrides,
  };
}

function buildAuditEntry(overrides: Partial<AuditEntry> = {}): AuditEntry {
  return {
    auditEntryId: 'audit-1',
    entityType: 'Client',
    entityId: 'client-1',
    action: 'Created',
    actionCategory: 'WRITE',
    actorUserId: 'owner-1',
    actorType: 'User',
    actorDisplayName: 'Jane Owner',
    sourceService: 'Crm',
    occurredAtUtc: '2026-08-10T12:00:00Z',
    auditedAtUtc: '2026-08-10T12:00:01Z',
    traceId: 'trace-123',
    correlationId: 'correlation-123',
    causationId: null,
    changedFields: '["name","lifecycleStatus"]',
    previousValues: null,
    newValues: null,
    summaryDescription: 'Client Acme Corp was created',
    ...overrides,
  };
}

function mockGetClient(impl: (...args: unknown[]) => unknown) {
  const mockFn = vi.fn(impl);
  vi.spyOn(clientsApiModule, 'clientsApi', 'get').mockReturnValue({
    getClient: mockFn,
  } as unknown as typeof clientsApiModule.clientsApi);
  return mockFn;
}

function mockGetEntriesByEntity(impl: (...args: unknown[]) => unknown) {
  const mockFn = vi.fn(impl);
  vi.spyOn(auditApiModule, 'auditApi', 'get').mockReturnValue({
    getEntriesByEntity: mockFn,
  } as unknown as typeof auditApiModule.auditApi);
  return mockFn;
}

function mockAuthenticatedUser(roles: string[]) {
  (useAuthModule.useAuth as any).mockReturnValue({
    currentUser: { userId: 'owner-1', email: 'owner@example.com', userName: 'owner', roles },
    isLoading: false,
    isAuthenticated: true,
    error: null,
    login: vi.fn(),
    logout: vi.fn(),
    refreshUser: vi.fn(),
  });
}

describe('ClientDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockClientId = 'client-1';
    mockAuthenticatedUser([]);
    // Default: activity fetch resolves with no entries when a test doesn't care about it.
    mockGetEntriesByEntity(() => Promise.resolve({ items: [], totalCount: 0 }));
  });

  it('shows a loading state while the client detail request is in flight', async () => {
    mockGetClient(() => new Promise(() => {}));

    render(<ClientDetailPage />);

    expect(screen.getByText(/Loading client/i)).toBeInTheDocument();
  });

  it('renders client information, lifecycle, owner, and Project/Task summaries on success', async () => {
    mockGetClient(() => Promise.resolve(buildClientDetail()));

    render(<ClientDetailPage />);

    expect(await screen.findByRole('heading', { name: 'Acme Corp', level: 1 })).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument(); // lifecycle badge
    expect(screen.getByText('owner-1')).toBeInTheDocument(); // assigned owner
    expect(screen.getByText('Website Redesign')).toBeInTheDocument(); // active project
    expect(screen.getByText('Draft wireframes')).toBeInTheDocument(); // open task
    expect(screen.getByText(/No historical projects for this client\./)).toBeInTheDocument();
    expect(screen.getByText(/No recently completed tasks\./)).toBeInTheDocument();
  });

  it('shows a not-found state and does not render project/task sections for a 404', async () => {
    mockGetClient(() => Promise.reject(new NotFoundError({ status: 404 })));

    render(<ClientDetailPage />);

    expect(await screen.findByText(/This client may have been archived, removed/i)).toBeInTheDocument();
    expect(screen.queryByText('Website Redesign')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /Back to clients/i }));
    expect(mockNavigate).toHaveBeenCalledWith('/clients');
  });

  it('shows an error state with retry for a non-404 failure, and retry re-fetches', async () => {
    const mockFn = mockGetClient(() => Promise.reject(new HttpError(500, {}, 'HTTP 500')));

    render(<ClientDetailPage />);

    expect(await screen.findByText(/Something went wrong/i)).toBeInTheDocument();
    expect(mockFn).toHaveBeenCalledTimes(1);

    mockFn.mockResolvedValueOnce(buildClientDetail());
    await userEvent.click(screen.getByRole('button', { name: /Try again/i }));

    expect(await screen.findByRole('heading', { name: 'Acme Corp', level: 1 })).toBeInTheDocument();
    expect(mockFn).toHaveBeenCalledTimes(2);
  });

  it('shows an empty state and does not call the API when no client ID is present in the route', () => {
    mockClientId = undefined;
    const mockFn = mockGetClient(() => Promise.resolve(buildClientDetail()));

    render(<ClientDetailPage />);

    expect(screen.getByText(/No client identifier was provided/i)).toBeInTheDocument();
    expect(mockFn).not.toHaveBeenCalled();
  });

  it('navigates to the client-scoped Projects and Tasks views (CLIENT-031/032)', async () => {
    mockGetClient(() => Promise.resolve(buildClientDetail()));

    render(<ClientDetailPage />);
    await screen.findByRole('heading', { name: 'Acme Corp', level: 1 });

    await userEvent.click(screen.getByRole('button', { name: /View all Projects/i }));
    expect(mockNavigate).toHaveBeenCalledWith('/projects?clientId=client-1');

    await userEvent.click(screen.getByRole('button', { name: /View all Tasks/i }));
    expect(mockNavigate).toHaveBeenCalledWith('/tasks?clientId=client-1');
  });

  describe('recent activity (ACTIVITY-001..003)', () => {
    it('does not call the audit API and shows an unavailable message for a user without Administrator/Manager role', async () => {
      mockAuthenticatedUser(['User']);
      mockGetClient(() => Promise.resolve(buildClientDetail()));
      const mockActivity = mockGetEntriesByEntity(() => Promise.resolve({ items: [], totalCount: 0 }));

      render(<ClientDetailPage />);
      await screen.findByRole('heading', { name: 'Acme Corp', level: 1 });

      expect(screen.getByText(/requires Administrator or Manager access/i)).toBeInTheDocument();
      expect(mockActivity).not.toHaveBeenCalled();
    });

    it('shows a friendly activity feed with an audit-details disclosure for an authorized user', async () => {
      mockAuthenticatedUser(['Manager']);
      mockGetClient(() => Promise.resolve(buildClientDetail()));
      mockGetEntriesByEntity(() =>
        Promise.resolve({ items: [buildAuditEntry()], totalCount: 1 }),
      );

      render(<ClientDetailPage />);

      expect(await screen.findByText('Client Acme Corp was created')).toBeInTheDocument();
      expect(screen.getByText(/by Jane Owner/)).toBeInTheDocument();

      await userEvent.click(screen.getByText('Audit details'));
      expect(screen.getByText('trace-123')).toBeInTheDocument();
      expect(screen.getByText('name, lifecycleStatus')).toBeInTheDocument();
    });

    it('shows an empty activity state when authorized but no entries exist', async () => {
      mockAuthenticatedUser(['Administrator']);
      mockGetClient(() => Promise.resolve(buildClientDetail()));
      mockGetEntriesByEntity(() => Promise.resolve({ items: [], totalCount: 0 }));

      render(<ClientDetailPage />);

      expect(await screen.findByText(/No audited events have been recorded/i)).toBeInTheDocument();
    });

    it('falls back to the unavailable message when the backend rejects with 403 despite the role hint', async () => {
      mockAuthenticatedUser(['Manager']);
      mockGetClient(() => Promise.resolve(buildClientDetail()));
      mockGetEntriesByEntity(() => Promise.reject(new AuthorizationError({ status: 403 })));

      render(<ClientDetailPage />);

      expect(await screen.findByText(/requires Administrator or Manager access/i)).toBeInTheDocument();
    });

    it('shows an error state with retry when the activity request fails for a reason other than authorization', async () => {
      mockAuthenticatedUser(['Manager']);
      mockGetClient(() => Promise.resolve(buildClientDetail()));
      const mockActivity = mockGetEntriesByEntity(() => Promise.reject(new HttpError(500, {}, 'HTTP 500')));

      render(<ClientDetailPage />);

      await waitFor(() => {
        expect(screen.getAllByText(/Something went wrong/i).length).toBeGreaterThan(0);
      });
      expect(mockActivity).toHaveBeenCalledTimes(1);
    });
  });
});
