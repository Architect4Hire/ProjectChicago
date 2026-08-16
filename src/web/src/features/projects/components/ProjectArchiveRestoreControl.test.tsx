import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ProjectArchiveRestoreControl } from './ProjectArchiveRestoreControl';
import * as projectsApi from '@/api/projects';
import * as authModule from '@/auth';
import { ConflictError, ValidationError } from '@/api';

vi.mock('@/api/projects');
vi.mock('@/auth');

const mockProject = {
  id: '1',
  clientId: 'client-1',
  clientName: 'Acme Corp',
  name: 'Website Redesign',
  description: 'Redesign company website',
  status: 'Active' as const,
  priority: 'High' as const,
  ownerUserId: 'user-1',
  startDateUtc: '2024-01-01T00:00:00Z',
  targetCompletionDateUtc: '2024-06-30T00:00:00Z',
  actualCompletionDateUtc: null,
  notes: 'High priority project',
  createdAtUtc: '2024-01-01T00:00:00Z',
  createdBy: 'admin',
  lastModifiedAtUtc: '2024-01-05T00:00:00Z',
  lastModifiedBy: 'admin',
  concurrencyToken: 'token-123',
};

describe('ProjectArchiveRestoreControl - Archive', () => {
  const mockOnChanged = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(authModule.useAuth).mockReturnValue({
      currentUser: { id: 'user-1', email: 'test@example.com', roles: ['Administrator'] },
      isLoading: false,
      isAuthenticated: true,
    } as any);
  });

  it('renders archive button for non-archived project', () => {
    render(<ProjectArchiveRestoreControl project={mockProject} onChanged={mockOnChanged} />);

    expect(screen.getByRole('button', { name: /archive project/i })).toBeInTheDocument();
  });

  it('opens confirmation dialog when archive button clicked', async () => {
    render(<ProjectArchiveRestoreControl project={mockProject} onChanged={mockOnChanged} />);

    const archiveButton = screen.getByRole('button', { name: /archive project/i });
    fireEvent.click(archiveButton);

    await waitFor(() => {
      expect(screen.getByText(/archive this project\?/i)).toBeInTheDocument();
    });
  });

  it('calls archiveProject API on confirmation', async () => {
    vi.mocked(projectsApi.projectsApi.archiveProject).mockResolvedValue({
      ...mockProject,
      status: 'Archived',
    });

    render(<ProjectArchiveRestoreControl project={mockProject} onChanged={mockOnChanged} />);

    const archiveButton = screen.getByRole('button', { name: /archive project/i });
    fireEvent.click(archiveButton);

    await waitFor(() => {
      expect(screen.getByText(/archive this project\?/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /^Archive$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(projectsApi.projectsApi.archiveProject).toHaveBeenCalledWith('1', {
        expectedConcurrencyToken: 'token-123',
      });
    });
  });

  it('calls onChanged callback after successful archive', async () => {
    vi.mocked(projectsApi.projectsApi.archiveProject).mockResolvedValue({
      ...mockProject,
      status: 'Archived',
    });

    render(<ProjectArchiveRestoreControl project={mockProject} onChanged={mockOnChanged} />);

    const archiveButton = screen.getByRole('button', { name: /archive project/i });
    fireEvent.click(archiveButton);

    await waitFor(() => {
      expect(screen.getByText(/archive this project\?/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /^Archive$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(mockOnChanged).toHaveBeenCalled();
    });
  });

  it('handles ConflictError with reload option', async () => {
    vi.mocked(projectsApi.projectsApi.archiveProject).mockRejectedValue(
      new ConflictError({
        status: 409,
        title: 'Conflict',
        detail: 'Concurrency conflict',
      })
    );

    render(<ProjectArchiveRestoreControl project={mockProject} onChanged={mockOnChanged} />);

    const archiveButton = screen.getByRole('button', { name: /archive project/i });
    fireEvent.click(archiveButton);

    await waitFor(() => {
      expect(screen.getByText(/archive this project\?/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /^Archive$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByText(/someone else changed this project/i)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /reload/i })).toBeInTheDocument();
    });
  });

  it('handles ValidationError with error message', async () => {
    vi.mocked(projectsApi.projectsApi.archiveProject).mockRejectedValue(
      new ValidationError(
        {
          status: 400,
          title: 'Validation Error',
          detail: 'This project could not be archived',
        },
        {}
      )
    );

    render(<ProjectArchiveRestoreControl project={mockProject} onChanged={mockOnChanged} />);

    const archiveButton = screen.getByRole('button', { name: /archive project/i });
    fireEvent.click(archiveButton);

    await waitFor(() => {
      expect(screen.getByText(/archive this project\?/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /^Archive$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByText(/this project could not be archived/i)).toBeInTheDocument();
    });
  });

  it('disables archive button while saving', async () => {
    vi.mocked(projectsApi.projectsApi.archiveProject).mockImplementation(
      () => new Promise((resolve) => setTimeout(() => resolve({ ...mockProject, status: 'Archived' }), 100))
    );

    render(<ProjectArchiveRestoreControl project={mockProject} onChanged={mockOnChanged} />);

    const archiveButton = screen.getByRole('button', { name: /archive project/i });
    fireEvent.click(archiveButton);

    await waitFor(() => {
      expect(screen.getByText(/archive this project\?/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /^Archive$/i });
    fireEvent.click(confirmButton);

    expect(confirmButton).toHaveAttribute('disabled');
  });
});

describe('ProjectArchiveRestoreControl - Restore', () => {
  const mockArchivedProject = {
    ...mockProject,
    status: 'Archived' as const,
  };
  const mockOnChanged = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders authorization message for unauthorized users', () => {
    vi.mocked(authModule.useAuth).mockReturnValue({
      currentUser: { id: 'user-1', email: 'test@example.com', roles: ['User'] },
      isLoading: false,
      isAuthenticated: true,
    } as any);

    render(<ProjectArchiveRestoreControl project={mockArchivedProject} onChanged={mockOnChanged} />);

    expect(screen.getByText(/archived.*restoring.*requires administrator/i)).toBeInTheDocument();
  });

  it('renders restore dropdown and button for authorized users', () => {
    vi.mocked(authModule.useAuth).mockReturnValue({
      currentUser: { id: 'user-1', email: 'test@example.com', roles: ['Administrator'] },
      isLoading: false,
      isAuthenticated: true,
    } as any);

    render(<ProjectArchiveRestoreControl project={mockArchivedProject} onChanged={mockOnChanged} />);

    expect(screen.getByLabelText(/restore to status/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /restore project/i })).toBeInTheDocument();
  });

  it('excludes Archived status from restore options', () => {
    vi.mocked(authModule.useAuth).mockReturnValue({
      currentUser: { id: 'user-1', email: 'test@example.com', roles: ['Manager'] },
      isLoading: false,
      isAuthenticated: true,
    } as any);

    render(<ProjectArchiveRestoreControl project={mockArchivedProject} onChanged={mockOnChanged} />);

    const select = screen.getByLabelText(/restore to status/i) as HTMLSelectElement;
    const options = Array.from(select.options).map((o) => o.value);

    expect(options).not.toContain('Archived');
  });

  it('opens confirmation dialog when restore button clicked', async () => {
    vi.mocked(authModule.useAuth).mockReturnValue({
      currentUser: { id: 'user-1', email: 'test@example.com', roles: ['Administrator'] },
      isLoading: false,
      isAuthenticated: true,
    } as any);

    render(<ProjectArchiveRestoreControl project={mockArchivedProject} onChanged={mockOnChanged} />);

    const restoreButton = screen.getByRole('button', { name: /restore project/i });
    fireEvent.click(restoreButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm project restore/i)).toBeInTheDocument();
    });
  });

  it('calls restoreProject API on confirmation', async () => {
    vi.mocked(authModule.useAuth).mockReturnValue({
      currentUser: { id: 'user-1', email: 'test@example.com', roles: ['Administrator'] },
      isLoading: false,
      isAuthenticated: true,
    } as any);

    vi.mocked(projectsApi.projectsApi.restoreProject).mockResolvedValue({
      ...mockArchivedProject,
      status: 'Active',
    });

    render(<ProjectArchiveRestoreControl project={mockArchivedProject} onChanged={mockOnChanged} />);

    const restoreButton = screen.getByRole('button', { name: /restore project/i });
    fireEvent.click(restoreButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm project restore/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /^Confirm$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(projectsApi.projectsApi.restoreProject).toHaveBeenCalledWith('1', {
        restoredStatus: 'Planned',
        expectedConcurrencyToken: 'token-123',
      });
    });
  });

  it('calls onChanged callback after successful restore', async () => {
    vi.mocked(authModule.useAuth).mockReturnValue({
      currentUser: { id: 'user-1', email: 'test@example.com', roles: ['Manager'] },
      isLoading: false,
      isAuthenticated: true,
    } as any);

    vi.mocked(projectsApi.projectsApi.restoreProject).mockResolvedValue({
      ...mockArchivedProject,
      status: 'Active',
    });

    render(<ProjectArchiveRestoreControl project={mockArchivedProject} onChanged={mockOnChanged} />);

    const restoreButton = screen.getByRole('button', { name: /restore project/i });
    fireEvent.click(restoreButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm project restore/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /^Confirm$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(mockOnChanged).toHaveBeenCalled();
    });
  });

  it('handles ConflictError with reload option', async () => {
    vi.mocked(authModule.useAuth).mockReturnValue({
      currentUser: { id: 'user-1', email: 'test@example.com', roles: ['Administrator'] },
      isLoading: false,
      isAuthenticated: true,
    } as any);

    vi.mocked(projectsApi.projectsApi.restoreProject).mockRejectedValue(
      new ConflictError({
        status: 409,
        title: 'Conflict',
        detail: 'Concurrency conflict',
      })
    );

    render(<ProjectArchiveRestoreControl project={mockArchivedProject} onChanged={mockOnChanged} />);

    const restoreButton = screen.getByRole('button', { name: /restore project/i });
    fireEvent.click(restoreButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm project restore/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /^Confirm$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByText(/someone else changed this project/i)).toBeInTheDocument();
    });
  });

  it('handles ValidationError with error message', async () => {
    vi.mocked(authModule.useAuth).mockReturnValue({
      currentUser: { id: 'user-1', email: 'test@example.com', roles: ['Administrator'] },
      isLoading: false,
      isAuthenticated: true,
    } as any);

    vi.mocked(projectsApi.projectsApi.restoreProject).mockRejectedValue(
      new ValidationError(
        {
          status: 400,
          title: 'Validation Error',
          detail: 'Invalid restore status',
        },
        { restoredStatus: ['Invalid restore status'] }
      )
    );

    render(<ProjectArchiveRestoreControl project={mockArchivedProject} onChanged={mockOnChanged} />);

    const restoreButton = screen.getByRole('button', { name: /restore project/i });
    fireEvent.click(restoreButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm project restore/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /^Confirm$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByText(/invalid restore status/i)).toBeInTheDocument();
    });
  });
});
