import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { ProjectsListPage } from './ProjectsListPage';
import * as projectsApi from '@/api/projects';

vi.mock('@/api/projects');

const mockProjects = [
  {
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
  },
];

const mockPagedResponse = {
  items: mockProjects,
  totalCount: 1,
  totalPages: 1,
  pageNumber: 1,
  pageSize: 10,
};

describe('ProjectsListPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders loading state initially', () => {
    vi.mocked(projectsApi.projectsApi.listProjects).mockImplementation(
      () => new Promise((resolve) => setTimeout(() => resolve(mockPagedResponse), 100))
    );

    render(
      <BrowserRouter>
        <ProjectsListPage />
      </BrowserRouter>
    );

    expect(screen.getByText(/loading projects/i)).toBeInTheDocument();
  });

  it('renders projects list when data loads', async () => {
    vi.mocked(projectsApi.projectsApi.listProjects).mockResolvedValue(mockPagedResponse);

    render(
      <BrowserRouter>
        <ProjectsListPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Website Redesign')).toBeInTheDocument();
    });

    expect(screen.getByText('Acme Corp')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
  });

  it('renders empty state when no projects', async () => {
    vi.mocked(projectsApi.projectsApi.listProjects).mockResolvedValue({
      items: [],
      totalCount: 0,
      totalPages: 0,
      pageNumber: 1,
      pageSize: 10,
    });

    render(
      <BrowserRouter>
        <ProjectsListPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/no projects found/i)).toBeInTheDocument();
    });
  });

  it('shows filter panel when toggle clicked', async () => {
    vi.mocked(projectsApi.projectsApi.listProjects).mockResolvedValue(mockPagedResponse);

    render(
      <BrowserRouter>
        <ProjectsListPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Website Redesign')).toBeInTheDocument();
    });

    const filterButton = screen.getByLabelText('Show filters');
    fireEvent.click(filterButton);

    await waitFor(() => {
      expect(screen.getByLabelText('Search projects')).toBeInTheDocument();
    });
  });

  it('filters projects by search term', async () => {
    vi.mocked(projectsApi.projectsApi.listProjects).mockResolvedValue(mockPagedResponse);

    render(
      <BrowserRouter>
        <ProjectsListPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Website Redesign')).toBeInTheDocument();
    });

    const filterButton = screen.getByLabelText('Show filters');
    fireEvent.click(filterButton);

    const searchInput = await screen.findByPlaceholderText(/search by project name/i);
    fireEvent.change(searchInput, { target: { value: 'Website' } });

    await waitFor(() => {
      expect(vi.mocked(projectsApi.projectsApi.listProjects)).toHaveBeenCalledWith(
        expect.objectContaining({
          search: 'Website',
        })
      );
    });
  });

  it('filters projects by status', async () => {
    vi.mocked(projectsApi.projectsApi.listProjects).mockResolvedValue(mockPagedResponse);

    render(
      <BrowserRouter>
        <ProjectsListPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Website Redesign')).toBeInTheDocument();
    });

    const filterButton = screen.getByLabelText('Show filters');
    fireEvent.click(filterButton);

    const statusCheckbox = await screen.findByLabelText('Filter by status Active');
    fireEvent.click(statusCheckbox);

    await waitFor(() => {
      expect(vi.mocked(projectsApi.projectsApi.listProjects)).toHaveBeenCalledWith(
        expect.objectContaining({
          status: ['Active'],
        })
      );
    });
  });

  it('is keyboard accessible for filters', async () => {
    vi.mocked(projectsApi.projectsApi.listProjects).mockResolvedValue(mockPagedResponse);

    render(
      <BrowserRouter>
        <ProjectsListPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Website Redesign')).toBeInTheDocument();
    });

    const filterButton = screen.getByLabelText('Show filters');
    filterButton.focus();
    expect(filterButton).toHaveFocus();

    fireEvent.keyDown(filterButton, { key: 'Enter' });
    fireEvent.click(filterButton);

    const searchInput = await screen.findByLabelText('Search projects');
    searchInput.focus();
    expect(searchInput).toHaveFocus();

    fireEvent.change(searchInput, { target: { value: 'Test' } });
    expect(searchInput).toHaveValue('Test');
  });

  it('handles pagination correctly', async () => {
    vi.mocked(projectsApi.projectsApi.listProjects).mockResolvedValue({
      items: mockProjects,
      totalCount: 30,
      totalPages: 3,
      pageNumber: 1,
      pageSize: 10,
    });

    render(
      <BrowserRouter>
        <ProjectsListPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Page 1 of 3')).toBeInTheDocument();
    });

    const nextButton = screen.getByLabelText('Next page');
    fireEvent.click(nextButton);

    await waitFor(() => {
      expect(vi.mocked(projectsApi.projectsApi.listProjects)).toHaveBeenCalledWith(
        expect.objectContaining({
          pageNumber: 2,
        })
      );
    });
  });

  it('handles sorting by column', async () => {
    vi.mocked(projectsApi.projectsApi.listProjects).mockResolvedValue(mockPagedResponse);

    render(
      <BrowserRouter>
        <ProjectsListPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Website Redesign')).toBeInTheDocument();
    });

    const projectNameHeader = screen.getByText(/project name/i);
    fireEvent.click(projectNameHeader);

    await waitFor(() => {
      expect(vi.mocked(projectsApi.projectsApi.listProjects)).toHaveBeenCalledWith(
        expect.objectContaining({
          sortBy: 'name',
          sortDirection: 'Ascending',
        })
      );
    });
  });

  it('displays error state on API failure', async () => {
    vi.mocked(projectsApi.projectsApi.listProjects).mockRejectedValue(new Error('Network error'));

    render(
      <BrowserRouter>
        <ProjectsListPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/something went wrong/i)).toBeInTheDocument();
    });
  });

  it('retries on error', async () => {
    vi.mocked(projectsApi.projectsApi.listProjects)
      .mockRejectedValueOnce(new Error('Network error'))
      .mockResolvedValueOnce(mockPagedResponse);

    render(
      <BrowserRouter>
        <ProjectsListPage />
      </BrowserRouter>
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
});
