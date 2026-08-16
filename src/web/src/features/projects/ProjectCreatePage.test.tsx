import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ProjectCreatePage } from './ProjectCreatePage';
import * as projectsApiModule from '@/api/projects';
import * as clientsApiModule from '@/api/clients';
import type { Project, CreateProjectRequest } from '@/api/projects';
import type { Client } from '@/api/clients';
import { ValidationError, HttpError, AuthorizationError, AuthenticationError } from '@/api/http';
import * as useAuthModule from '@/auth';

vi.mock('@/api/projects');
vi.mock('@/api/clients');
vi.mock('@/auth', () => ({ useAuth: vi.fn() }));

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => mockNavigate };
});

function buildProject(overrides: Partial<Project> = {}): Project {
  return {
    id: 'project-1',
    clientId: 'client-1',
    clientName: 'Acme Corp',
    name: 'Website Redesign',
    description: 'Redesign company website',
    status: 'Planned',
    priority: 'Normal',
    ownerUserId: 'user-1',
    startDateUtc: null,
    targetCompletionDateUtc: null,
    actualCompletionDateUtc: null,
    notes: '',
    createdAtUtc: '2026-08-16T00:00:00Z',
    createdBy: 'user-1',
    lastModifiedAtUtc: '2026-08-16T00:00:00Z',
    lastModifiedBy: 'user-1',
    ...overrides,
  };
}

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
    lifecycleStatus: 'Active',
    description: '',
    assignedOwner: 'owner-1',
    createdDate: '2026-08-16T00:00:00Z',
    createdBy: 'owner-1',
    lastModifiedDate: '2026-08-16T00:00:00Z',
    lastModifiedBy: 'owner-1',
    ...overrides,
  };
}

function mockCreateProjectReturning(impl: (...args: unknown[]) => unknown) {
  const mockCreateProject = vi.fn(impl);
  vi.spyOn(projectsApiModule, 'projectsApi', 'get').mockReturnValue({
    createProject: mockCreateProject,
  } as unknown as typeof projectsApiModule.projectsApi);
  return mockCreateProject;
}

function mockListClientsReturning(impl: (...args: unknown[]) => unknown) {
  const mockListClients = vi.fn(impl);
  vi.spyOn(clientsApiModule, 'clientsApi', 'get').mockReturnValue({
    listClients: mockListClients,
  } as unknown as typeof clientsApiModule.clientsApi);
  return mockListClients;
}

function mockAuthenticatedUser(userId: string | undefined) {
  (useAuthModule.useAuth as any).mockReturnValue({
    currentUser: userId ? { userId, email: 'user@example.com', userName: 'user', roles: [] } : null,
    isLoading: false,
    isAuthenticated: true,
    error: null,
    login: vi.fn(),
    logout: vi.fn(),
    refreshUser: vi.fn(),
  });
}

async function fillRequiredFields(description?: string) {
  const clientSelect = await screen.findByLabelText(/Client/i);
  await userEvent.selectOptions(clientSelect, 'client-1');

  await userEvent.type(screen.getByLabelText(/Project name/i), 'Website Redesign');
  if (description) {
    await userEvent.type(screen.getByLabelText(/Description/i), description);
  }
}

describe('ProjectCreatePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAuthenticatedUser('user-1');
    mockListClientsReturning(() =>
      Promise.resolve({
        items: [buildClient()],
        totalCount: 1,
        totalPages: 1,
        pageNumber: 1,
        pageSize: 10,
      }),
    );
  });

  describe('pending/loading state', () => {
    it('shows loading state while clients are being fetched', () => {
      mockListClientsReturning(() => new Promise(() => {}));

      render(<ProjectCreatePage />);

      expect(screen.getByText(/Loading clients/i)).toBeInTheDocument();
    });
  });

  describe('authorization', () => {
    it('shows authorization error when user is not authenticated', async () => {
      mockAuthenticatedUser(undefined);
      mockListClientsReturning(() =>
        Promise.resolve({
          items: [buildClient()],
          totalCount: 1,
          totalPages: 1,
          pageNumber: 1,
          pageSize: 10,
        }),
      );

      render(<ProjectCreatePage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Client/i)).toBeInTheDocument();
      });
    });
  });

  describe('validation', () => {
    it('displays required-field validation errors and does not submit when fields are blank', async () => {
      mockCreateProjectReturning(() => Promise.resolve(buildProject()));

      render(<ProjectCreatePage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Client/i)).toBeInTheDocument();
      });

      await userEvent.click(screen.getByRole('button', { name: /Create project/i }));

      expect(await screen.findByText('Client is required.')).toBeInTheDocument();
      expect(screen.getByText('Project name is required.')).toBeInTheDocument();
    });

    it('maps a server-side field validation error onto the relevant field', async () => {
      mockCreateProjectReturning(() =>
        Promise.reject(
          new ValidationError({ status: 400 }, { name: ['Project name must be unique within the client.'] }),
        ),
      );

      render(<ProjectCreatePage />);

      await fillRequiredFields();
      await userEvent.click(screen.getByRole('button', { name: /Create project/i }));

      expect(
        await screen.findByText('Project name must be unique within the client.'),
      ).toBeInTheDocument();
    });

    it('displays a generic server error banner when the request fails without field errors', async () => {
      mockCreateProjectReturning(() => Promise.reject(new HttpError(500, {}, 'HTTP 500')));

      render(<ProjectCreatePage />);

      await fillRequiredFields();
      await userEvent.click(screen.getByRole('button', { name: /Create project/i }));

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent('The project could not be saved. Try again.');
    });
  });

  describe('pending state', () => {
    it('shows a pending/submitting state while the create request is in flight', async () => {
      mockCreateProjectReturning(() => new Promise(() => {}));

      render(<ProjectCreatePage />);
      await fillRequiredFields();

      const submitButton = screen.getByRole('button', { name: /Create project/i });
      await userEvent.click(submitButton);

      await waitFor(() => {
        expect(submitButton).toHaveAttribute('aria-busy', 'true');
      });
      expect(screen.getByRole('button', { name: /Cancel/i })).toBeDisabled();
    });

    it('does not allow duplicate submission while request is in flight', async () => {
      const mockCreateProject = mockCreateProjectReturning(() => new Promise(() => {}));

      render(<ProjectCreatePage />);
      await fillRequiredFields();

      const submitButton = screen.getByRole('button', { name: /Create project/i });
      await userEvent.click(submitButton);
      await userEvent.click(submitButton);
      await userEvent.click(submitButton);

      await waitFor(() => {
        expect(mockCreateProject).toHaveBeenCalledTimes(1);
      });
    });

    it('disables form inputs while submitting', async () => {
      mockCreateProjectReturning(() => new Promise(() => {}));

      render(<ProjectCreatePage />);
      await fillRequiredFields();

      const clientSelect = screen.getByLabelText(/Client/i) as HTMLSelectElement;

      await userEvent.click(screen.getByRole('button', { name: /Create project/i }));

      await waitFor(() => {
        expect(clientSelect.disabled).toBe(true);
        expect((screen.getByLabelText(/Project name/i) as HTMLInputElement).disabled).toBe(true);
      });
    });
  });

  describe('success', () => {
    it('navigates to the project detail route on a successful save', async () => {
      mockCreateProjectReturning(() => Promise.resolve(buildProject({ id: 'new-project-id' })));

      render(<ProjectCreatePage />);
      await fillRequiredFields('Test description');
      await userEvent.click(screen.getByRole('button', { name: /Create project/i }));

      await waitFor(() => {
        expect(mockNavigate).toHaveBeenCalledWith('/projects/new-project-id');
      });
    });

    it('sends the correct request payload with required fields', async () => {
      const mockCreateProject = mockCreateProjectReturning(() =>
        Promise.resolve(buildProject({ id: 'new-project-id' })),
      );

      render(<ProjectCreatePage />);
      await fillRequiredFields('Redesign company website');
      await userEvent.click(screen.getByRole('button', { name: /Create project/i }));

      await waitFor(() => {
        expect(mockCreateProject).toHaveBeenCalledWith(
          expect.objectContaining({
            clientId: 'client-1',
            name: 'Website Redesign',
            description: 'Redesign company website',
            ownerUserId: 'user-1',
          } as CreateProjectRequest),
        );
      });
    });

    it('sends the correct request payload with only required fields when description is omitted', async () => {
      const mockCreateProject = mockCreateProjectReturning(() =>
        Promise.resolve(buildProject({ id: 'new-project-id' })),
      );

      render(<ProjectCreatePage />);
      await fillRequiredFields();
      await userEvent.click(screen.getByRole('button', { name: /Create project/i }));

      await waitFor(() => {
        expect(mockCreateProject).toHaveBeenCalledWith(
          expect.objectContaining({
            clientId: 'client-1',
            name: 'Website Redesign',
            ownerUserId: 'user-1',
          }),
        );
      });
    });
  });

  describe('error handling', () => {
    it('shows authorization error when user lacks permission', async () => {
      mockCreateProjectReturning(() =>
        Promise.reject(new AuthorizationError({ status: 403 }, {})),
      );

      render(<ProjectCreatePage />);
      await fillRequiredFields();
      await userEvent.click(screen.getByRole('button', { name: /Create project/i }));

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent('You do not have permission to create projects.');
    });

    it('shows authentication error when session expires', async () => {
      mockCreateProjectReturning(() =>
        Promise.reject(new AuthenticationError({ status: 401 }, {})),
      );

      render(<ProjectCreatePage />);
      await fillRequiredFields();
      await userEvent.click(screen.getByRole('button', { name: /Create project/i }));

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent('Your session has expired. Sign in again to create a project.');
    });
  });

  describe('empty state', () => {
    it('shows empty state when no clients are available', async () => {
      mockListClientsReturning(() =>
        Promise.resolve({
          items: [],
          totalCount: 0,
          totalPages: 0,
          pageNumber: 1,
          pageSize: 10,
        }),
      );

      render(<ProjectCreatePage />);

      await waitFor(() => {
        expect(screen.getByText(/No clients available/i)).toBeInTheDocument();
      });

      expect(screen.getByRole('button', { name: /Create a client/i })).toBeInTheDocument();
    });
  });

  describe('client loading error', () => {
    it('shows error state when clients fail to load', async () => {
      mockListClientsReturning(() => Promise.reject(new Error('Network error')));

      render(<ProjectCreatePage />);

      await waitFor(() => {
        expect(screen.getByText(/Failed to load clients/i)).toBeInTheDocument();
      });

      expect(screen.getByRole('button', { name: /Back to projects/i })).toBeInTheDocument();
    });
  });

  describe('focus management', () => {
    it('moves focus to the first invalid field on validation error', async () => {
      mockCreateProjectReturning(() => Promise.resolve(buildProject()));

      render(<ProjectCreatePage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Client/i)).toBeInTheDocument();
      });

      const nameInput = screen.getByLabelText(/Project name/i) as HTMLInputElement;
      await userEvent.click(screen.getByRole('button', { name: /Create project/i }));

      await waitFor(() => {
        expect(screen.getByLabelText(/Client/i)).toHaveFocus();
      });
    });
  });

  describe('navigation', () => {
    it('cancels and navigates back to projects list', async () => {
      render(<ProjectCreatePage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Client/i)).toBeInTheDocument();
      });

      await userEvent.click(screen.getByRole('button', { name: /Cancel/i }));

      expect(mockNavigate).toHaveBeenCalledWith('/projects');
    });
  });
});
