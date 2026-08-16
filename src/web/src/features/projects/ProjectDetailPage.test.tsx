import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { Routes, Route } from 'react-router-dom';
import { ProjectDetailPage } from './ProjectDetailPage';
import * as projectsApi from '@/api/projects';

vi.mock('@/api/projects');

const mockProject = {
  project: {
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
  },
  openTasks: [
    {
      id: 'task-1',
      title: 'Design mockups',
      status: 'InProgress' as const,
      priority: 'High' as const,
      assignedUserId: 'user-1',
      dueDateUtc: '2024-02-01T00:00:00Z',
      completedAtUtc: null,
    },
  ],
  completedTasks: [
    {
      id: 'task-2',
      title: 'Research competitors',
      status: 'Completed' as const,
      priority: 'Normal' as const,
      assignedUserId: 'user-1',
      dueDateUtc: '2024-01-15T00:00:00Z',
      completedAtUtc: '2024-01-14T00:00:00Z',
    },
  ],
};

describe('ProjectDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders loading state initially', () => {
    vi.mocked(projectsApi.projectsApi.getProject).mockImplementation(
      () => new Promise((resolve) => setTimeout(() => resolve(mockProject), 100))
    );

    render(
      <MemoryRouter initialEntries={['/projects/1']}>
        <Routes>
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByText(/loading project/i)).toBeInTheDocument();
  });

  it('renders project detail when data loads', async () => {
    vi.mocked(projectsApi.projectsApi.getProject).mockResolvedValue(mockProject);

    render(
      <MemoryRouter initialEntries={['/projects/1']}>
        <Routes>
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Website Redesign')).toBeInTheDocument();
    });

    expect(screen.getByText('Acme Corp')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getByText('High')).toBeInTheDocument();
  });

  it('renders project description and notes', async () => {
    vi.mocked(projectsApi.projectsApi.getProject).mockResolvedValue(mockProject);

    render(
      <MemoryRouter initialEntries={['/projects/1']}>
        <Routes>
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Website Redesign')).toBeInTheDocument();
    });

    expect(screen.getByText('Redesign company website')).toBeInTheDocument();
    expect(screen.getByText('High priority project')).toBeInTheDocument();
  });

  it('renders open tasks section', async () => {
    vi.mocked(projectsApi.projectsApi.getProject).mockResolvedValue(mockProject);

    render(
      <MemoryRouter initialEntries={['/projects/1']}>
        <Routes>
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Website Redesign')).toBeInTheDocument();
    });

    expect(screen.getByText(/Open Tasks \(1\)/)).toBeInTheDocument();
    expect(screen.getByText('Design mockups')).toBeInTheDocument();
  });

  it('renders completed tasks section', async () => {
    vi.mocked(projectsApi.projectsApi.getProject).mockResolvedValue(mockProject);

    render(
      <MemoryRouter initialEntries={['/projects/1']}>
        <Routes>
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Website Redesign')).toBeInTheDocument();
    });

    expect(screen.getByText(/Completed Tasks \(1\)/)).toBeInTheDocument();
    expect(screen.getByText('Research competitors')).toBeInTheDocument();
  });

  it('renders not found state when project not found', async () => {
    vi.mocked(projectsApi.projectsApi.getProject).mockRejectedValue(new Error('404 Not Found'));

    render(
      <MemoryRouter initialEntries={['/projects/999']}>
        <Routes>
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/project not found/i)).toBeInTheDocument();
    });
  });

  it('renders error state on API failure', async () => {
    vi.mocked(projectsApi.projectsApi.getProject).mockRejectedValue(new Error('Network error'));

    render(
      <MemoryRouter initialEntries={['/projects/1']}>
        <Routes>
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/something went wrong/i)).toBeInTheDocument();
    });
  });

  it('handles missing project ID', () => {
    render(
      <MemoryRouter initialEntries={['/projects/undefined']}>
        <Routes>
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByText(/project not found/i)).toBeInTheDocument();
  });

  it('is keyboard accessible', async () => {
    vi.mocked(projectsApi.projectsApi.getProject).mockResolvedValue(mockProject);

    render(
      <MemoryRouter initialEntries={['/projects/1']}>
        <Routes>
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Website Redesign')).toBeInTheDocument();
    });

    const backButton = screen.getByRole('button', { name: /back to projects/i });
    backButton.focus();
    expect(backButton).toHaveFocus();

    fireEvent.keyDown(backButton, { key: 'Enter' });
    fireEvent.click(backButton);
  });

  it('retries on error', async () => {
    vi.mocked(projectsApi.projectsApi.getProject)
      .mockRejectedValueOnce(new Error('Network error'))
      .mockResolvedValueOnce(mockProject);

    render(
      <MemoryRouter initialEntries={['/projects/1']}>
        <Routes>
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/something went wrong/i)).toBeInTheDocument();
    });

    const retryButton = screen.getByRole('button', { name: /try again/i });
    fireEvent.click(retryButton);

    await waitFor(() => {
      expect(screen.getByText('Website Redesign')).toBeInTheDocument();
    });
  });

  it('displays project metadata', async () => {
    vi.mocked(projectsApi.projectsApi.getProject).mockResolvedValue(mockProject);

    render(
      <MemoryRouter initialEntries={['/projects/1']}>
        <Routes>
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Website Redesign')).toBeInTheDocument();
    });

    expect(screen.getByText(/admin/)).toBeInTheDocument();
    expect(screen.getByText(/Jan 1, 2024/)).toBeInTheDocument();
  });
});
