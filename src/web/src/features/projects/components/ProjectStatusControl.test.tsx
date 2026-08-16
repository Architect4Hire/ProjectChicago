import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ProjectStatusControl } from './ProjectStatusControl';
import * as projectsApi from '@/api/projects';
import { ConflictError, ValidationError } from '@/api';

vi.mock('@/api/projects');

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

describe('ProjectStatusControl', () => {
  const mockOnStatusChanged = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders status dropdown for non-archived project', () => {
    render(<ProjectStatusControl project={mockProject} openTasksCount={0} onStatusChanged={mockOnStatusChanged} />);

    expect(screen.getByLabelText(/change status/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /change status/i })).toBeInTheDocument();
  });

  it('does not render for archived project', () => {
    const archivedProject = { ...mockProject, status: 'Archived' as const };
    render(<ProjectStatusControl project={archivedProject} openTasksCount={0} onStatusChanged={mockOnStatusChanged} />);

    expect(screen.getByText(/archived/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /change status/i })).not.toBeInTheDocument();
  });

  it('excludes current status from dropdown options', () => {
    render(<ProjectStatusControl project={mockProject} openTasksCount={0} onStatusChanged={mockOnStatusChanged} />);

    const select = screen.getByLabelText(/change status/i) as HTMLSelectElement;
    const options = Array.from(select.options).map((o) => o.value);

    expect(options).not.toContain('Active');
  });

  it('opens confirmation dialog on status change click', async () => {
    render(<ProjectStatusControl project={mockProject} openTasksCount={0} onStatusChanged={mockOnStatusChanged} />);

    const changeButton = screen.getByRole('button', { name: /change status/i });
    fireEvent.click(changeButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm status change/i)).toBeInTheDocument();
    });
  });

  it('shows open tasks acknowledgement when changing to Completed with open tasks', async () => {
    render(<ProjectStatusControl project={mockProject} openTasksCount={2} onStatusChanged={mockOnStatusChanged} />);

    const select = screen.getByLabelText(/change status/i) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'Completed' } });

    const changeButton = screen.getByRole('button', { name: /change status/i });
    fireEvent.click(changeButton);

    await waitFor(() => {
      expect(screen.getByText(/open tasks remain/i)).toBeInTheDocument();
      expect(screen.getByText(/2 open tasks/i)).toBeInTheDocument();
    });
  });

  it('shows confirmation dialog after acknowledging open tasks', async () => {
    render(<ProjectStatusControl project={mockProject} openTasksCount={1} onStatusChanged={mockOnStatusChanged} />);

    const select = screen.getByLabelText(/change status/i) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'Completed' } });

    const changeButton = screen.getByRole('button', { name: /change status/i });
    fireEvent.click(changeButton);

    await waitFor(() => {
      expect(screen.getByText(/open tasks remain/i)).toBeInTheDocument();
    });

    const continueButton = screen.getByRole('button', { name: /continue to completion/i });
    fireEvent.click(continueButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm status change/i)).toBeInTheDocument();
    });
  });

  it('does not show task ack dialog when no open tasks exist', async () => {
    render(<ProjectStatusControl project={mockProject} openTasksCount={0} onStatusChanged={mockOnStatusChanged} />);

    const select = screen.getByLabelText(/change status/i) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'Completed' } });

    const changeButton = screen.getByRole('button', { name: /change status/i });
    fireEvent.click(changeButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm status change/i)).toBeInTheDocument();
      expect(screen.queryByText(/open tasks remain/i)).not.toBeInTheDocument();
    });
  });

  it('calls changeStatus API on confirmation', async () => {
    vi.mocked(projectsApi.projectsApi.changeStatus).mockResolvedValue({
      ...mockProject,
      status: 'Planned',
    });

    render(<ProjectStatusControl project={mockProject} openTasksCount={0} onStatusChanged={mockOnStatusChanged} />);

    const select = screen.getByLabelText(/change status/i) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'Planned' } });

    const changeButton = screen.getByRole('button', { name: /change status/i });
    fireEvent.click(changeButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm status change/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /confirm$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(projectsApi.projectsApi.changeStatus).toHaveBeenCalledWith('1', {
        newStatus: 'Planned',
        expectedConcurrencyToken: 'token-123',
        acknowledgeOpenTasks: false,
      });
    });
  });

  it('passes acknowledgeOpenTasks flag when changing to Completed with open tasks', async () => {
    vi.mocked(projectsApi.projectsApi.changeStatus).mockResolvedValue({
      ...mockProject,
      status: 'Completed',
    });

    render(<ProjectStatusControl project={mockProject} openTasksCount={1} onStatusChanged={mockOnStatusChanged} />);

    const select = screen.getByLabelText(/change status/i) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'Completed' } });

    const changeButton = screen.getByRole('button', { name: /change status/i });
    fireEvent.click(changeButton);

    await waitFor(() => {
      expect(screen.getByText(/open tasks remain/i)).toBeInTheDocument();
    });

    const continueButton = screen.getByRole('button', { name: /continue to completion/i });
    fireEvent.click(continueButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm status change/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /confirm$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(projectsApi.projectsApi.changeStatus).toHaveBeenCalledWith('1', {
        newStatus: 'Completed',
        expectedConcurrencyToken: 'token-123',
        acknowledgeOpenTasks: true,
      });
    });
  });

  it('calls onStatusChanged callback on success', async () => {
    vi.mocked(projectsApi.projectsApi.changeStatus).mockResolvedValue({
      ...mockProject,
      status: 'Planned',
    });

    render(<ProjectStatusControl project={mockProject} openTasksCount={0} onStatusChanged={mockOnStatusChanged} />);

    const select = screen.getByLabelText(/change status/i) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'Planned' } });

    const changeButton = screen.getByRole('button', { name: /change status/i });
    fireEvent.click(changeButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm status change/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /confirm$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(mockOnStatusChanged).toHaveBeenCalled();
    });
  });

  it('handles ConflictError with reload option', async () => {
    vi.mocked(projectsApi.projectsApi.changeStatus).mockRejectedValue(
      new ConflictError({
        status: 409,
        title: 'Conflict',
        detail: 'Concurrency conflict',
      })
    );

    render(<ProjectStatusControl project={mockProject} openTasksCount={0} onStatusChanged={mockOnStatusChanged} />);

    const select = screen.getByLabelText(/change status/i) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'Planned' } });

    const changeButton = screen.getByRole('button', { name: /change status/i });
    fireEvent.click(changeButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm status change/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /confirm$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByText(/someone else changed this project/i)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /reload/i })).toBeInTheDocument();
    });
  });

  it('handles ValidationError with error message', async () => {
    vi.mocked(projectsApi.projectsApi.changeStatus).mockRejectedValue(
      new ValidationError(
        {
          status: 400,
          title: 'Validation Error',
          detail: 'This status change is not allowed',
        },
        { newStatus: ['This status transition is not allowed'] }
      )
    );

    render(<ProjectStatusControl project={mockProject} openTasksCount={0} onStatusChanged={mockOnStatusChanged} />);

    const select = screen.getByLabelText(/change status/i) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'Planned' } });

    const changeButton = screen.getByRole('button', { name: /change status/i });
    fireEvent.click(changeButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm status change/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /confirm$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByText(/this status transition is not allowed/i)).toBeInTheDocument();
    });
  });

  it('disables button while saving', async () => {
    vi.mocked(projectsApi.projectsApi.changeStatus).mockImplementation(
      () => new Promise((resolve) => setTimeout(() => resolve({ ...mockProject, status: 'Planned' }), 100))
    );

    render(<ProjectStatusControl project={mockProject} openTasksCount={0} onStatusChanged={mockOnStatusChanged} />);

    const select = screen.getByLabelText(/change status/i) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'Planned' } });

    const changeButton = screen.getByRole('button', { name: /change status/i });
    fireEvent.click(changeButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm status change/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /confirm$/i });
    fireEvent.click(confirmButton);

    expect(confirmButton).toHaveAttribute('disabled');
  });

  it('is keyboard accessible', async () => {
    render(<ProjectStatusControl project={mockProject} openTasksCount={0} onStatusChanged={mockOnStatusChanged} />);

    const select = screen.getByLabelText(/change status/i) as HTMLSelectElement;
    select.focus();
    expect(select).toHaveFocus();

    const changeButton = screen.getByRole('button', { name: /change status/i });
    changeButton.focus();
    expect(changeButton).toHaveFocus();

    fireEvent.keyDown(changeButton, { key: 'Enter' });
    fireEvent.click(changeButton);

    await waitFor(() => {
      expect(screen.getByText(/confirm status change/i)).toBeInTheDocument();
    });
  });
});
