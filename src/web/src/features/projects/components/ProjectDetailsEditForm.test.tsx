import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ProjectDetailsEditForm } from './ProjectDetailsEditForm';
import { projectsApi } from '@/api/projects';
import { ConflictError, ValidationError } from '@/api';
import type { ProjectDetailRecord } from '@/api/projects';

// Mock the API
vi.mock('@/api/projects', () => ({
  projectsApi: {
    updateProject: vi.fn(),
  },
}));

const mockProject: ProjectDetailRecord = {
  id: 'proj-123',
  clientId: 'client-123',
  clientName: 'Test Client',
  name: 'Test Project',
  description: 'Test Description',
  status: 'Active' as const,
  priority: 'High' as const,
  ownerUserId: 'user-123',
  startDateUtc: '2024-01-15T00:00:00Z',
  targetCompletionDateUtc: '2024-06-30T00:00:00Z',
  actualCompletionDateUtc: null,
  notes: 'Test notes',
  createdAtUtc: '2024-01-01T00:00:00Z',
  createdBy: 'creator',
  lastModifiedAtUtc: '2024-01-10T00:00:00Z',
  lastModifiedBy: 'modifier',
  concurrencyToken: 'token-123',
};

describe('ProjectDetailsEditForm', () => {
  const mockOnSaved = vi.fn();
  const mockOnCancel = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('rendering', () => {
    it('renders form fields with project data', () => {
      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      expect(screen.getByDisplayValue('Test Project')).toBeInTheDocument();
      expect(screen.getByDisplayValue('Test Description')).toBeInTheDocument();
      expect(screen.getByDisplayValue('user-123')).toBeInTheDocument();
      expect(screen.getByDisplayValue('2024-01-15')).toBeInTheDocument();
      expect(screen.getByDisplayValue('2024-06-30')).toBeInTheDocument();
      expect(screen.getByDisplayValue('Test notes')).toBeInTheDocument();
    });

    it('renders priority select with current priority selected', () => {
      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const prioritySelect = screen.getByDisplayValue('High');
      expect(prioritySelect).toBeInTheDocument();
    });

    it('renders Save changes and Cancel buttons', () => {
      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      expect(screen.getByRole('button', { name: /Save changes/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Cancel/i })).toBeInTheDocument();
    });

    it('displays required field indicators', () => {
      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const requiredIndicators = screen.getAllByText('*');
      expect(requiredIndicators.length).toBeGreaterThan(0);
    });

    it('handles null/empty optional fields', () => {
      const projectWithNulls: ProjectDetailRecord = {
        ...mockProject,
        description: null,
        notes: null,
        startDateUtc: null,
        targetCompletionDateUtc: null,
      };

      render(
        <ProjectDetailsEditForm
          project={projectWithNulls}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      expect((screen.getByPlaceholderText(/Describe the project/) as HTMLTextAreaElement).value).toBe('');
      expect((screen.getByPlaceholderText(/Additional notes/) as HTMLTextAreaElement).value).toBe('');
      expect((screen.getByDisplayValue('2024-01-15') as HTMLInputElement).value).toBe('');
      expect((screen.getByDisplayValue('2024-06-30') as HTMLInputElement).value).toBe('');
    });
  });

  describe('edit success', () => {
    it('saves changes and calls onSaved callback', async () => {
      const user = userEvent.setup();
      const mockResponse = {
        id: mockProject.id,
        clientId: mockProject.clientId,
        clientName: mockProject.clientName,
        name: 'Updated Project',
        description: mockProject.description || '',
        status: mockProject.status,
        priority: mockProject.priority,
        ownerUserId: mockProject.ownerUserId,
        startDateUtc: mockProject.startDateUtc,
        targetCompletionDateUtc: mockProject.targetCompletionDateUtc,
        actualCompletionDateUtc: mockProject.actualCompletionDateUtc,
        notes: mockProject.notes || '',
        createdAtUtc: mockProject.createdAtUtc,
        createdBy: mockProject.createdBy,
        lastModifiedAtUtc: mockProject.lastModifiedAtUtc,
        lastModifiedBy: mockProject.lastModifiedBy,
      };
      vi.mocked(projectsApi.updateProject).mockResolvedValue(mockResponse);

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const nameInput = screen.getByDisplayValue('Test Project');
      await user.clear(nameInput);
      await user.type(nameInput, 'Updated Project');

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(vi.mocked(projectsApi.updateProject)).toHaveBeenCalledWith('proj-123', {
          name: 'Updated Project',
        });
      });

      // Wait for success message
      await waitFor(() => {
        expect(screen.getByRole('status', { hidden: false })).toHaveTextContent('Project details saved successfully');
      });

      // Wait for callback
      await waitFor(() => {
        expect(mockOnSaved).toHaveBeenCalled();
      });
    });

    it('only sends changed fields to API', async () => {
      const user = userEvent.setup();
      const mockResponse = {
        id: mockProject.id,
        clientId: mockProject.clientId,
        clientName: mockProject.clientName,
        name: mockProject.name,
        description: mockProject.description || '',
        status: mockProject.status,
        priority: 'Critical' as const,
        ownerUserId: mockProject.ownerUserId,
        startDateUtc: mockProject.startDateUtc,
        targetCompletionDateUtc: mockProject.targetCompletionDateUtc,
        actualCompletionDateUtc: mockProject.actualCompletionDateUtc,
        notes: mockProject.notes || '',
        createdAtUtc: mockProject.createdAtUtc,
        createdBy: mockProject.createdBy,
        lastModifiedAtUtc: mockProject.lastModifiedAtUtc,
        lastModifiedBy: mockProject.lastModifiedBy,
      };
      vi.mocked(projectsApi.updateProject).mockResolvedValue(mockResponse);

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const prioritySelect = screen.getByDisplayValue('High');
      await user.selectOptions(prioritySelect, 'Critical');

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(vi.mocked(projectsApi.updateProject)).toHaveBeenCalledWith('proj-123', {
          priority: 'Critical',
        });
      });
    });

    it('handles no changes gracefully', async () => {
      const user = userEvent.setup();

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      // Should not call API if no changes
      await waitFor(() => {
        expect(vi.mocked(projectsApi.updateProject)).not.toHaveBeenCalled();
      });

      // Should show success message anyway
      await waitFor(() => {
        expect(screen.getByRole('status', { hidden: false })).toHaveTextContent('Project details saved successfully');
      });

      await waitFor(() => {
        expect(mockOnSaved).toHaveBeenCalled();
      });
    });

    it('converts dates to ISO format correctly', async () => {
      const user = userEvent.setup();
      const mockResponse = {
        id: mockProject.id,
        clientId: mockProject.clientId,
        clientName: mockProject.clientName,
        name: mockProject.name,
        description: mockProject.description || '',
        status: mockProject.status,
        priority: mockProject.priority,
        ownerUserId: mockProject.ownerUserId,
        startDateUtc: '2025-03-15T00:00:00Z',
        targetCompletionDateUtc: '2025-06-30T00:00:00Z',
        actualCompletionDateUtc: mockProject.actualCompletionDateUtc,
        notes: mockProject.notes || '',
        createdAtUtc: mockProject.createdAtUtc,
        createdBy: mockProject.createdBy,
        lastModifiedAtUtc: mockProject.lastModifiedAtUtc,
        lastModifiedBy: mockProject.lastModifiedBy,
      };
      vi.mocked(projectsApi.updateProject).mockResolvedValue(mockResponse);

      render(
        <ProjectDetailsEditForm
          project={{ ...mockProject, startDateUtc: null, targetCompletionDateUtc: null }}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const startDateInput = screen.getByLabelText(/^Start date/);
      await user.type(startDateInput, '2025-03-15');

      const targetDateInput = screen.getByLabelText(/^Target completion/);
      await user.type(targetDateInput, '2025-06-30');

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        const call = vi.mocked(projectsApi.updateProject).mock.calls[0];
        expect(call[1].startDateUtc).toBe('2025-03-15T00:00:00Z');
        expect(call[1].targetCompletionDateUtc).toBe('2025-06-30T00:00:00Z');
      });
    });
  });

  describe('invalid dates', () => {
    it('shows error when start date is after target completion date', async () => {
      const user = userEvent.setup();

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const startDateInput = screen.getByLabelText(/^Start date/);
      await user.clear(startDateInput);
      await user.type(startDateInput, '2025-12-31');

      const targetDateInput = screen.getByLabelText(/^Target completion/);
      await user.clear(targetDateInput);
      await user.type(targetDateInput, '2025-01-01');

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/Start date must be before target completion date/)).toBeInTheDocument();
      });

      expect(vi.mocked(projectsApi.updateProject)).not.toHaveBeenCalled();
    });

    it('shows error when required name field is empty', async () => {
      const user = userEvent.setup();

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const nameInput = screen.getByDisplayValue('Test Project');
      await user.clear(nameInput);

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/Project name is required/)).toBeInTheDocument();
      });

      expect(vi.mocked(projectsApi.updateProject)).not.toHaveBeenCalled();
    });

    it('shows error when required owner field is empty', async () => {
      const user = userEvent.setup();

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const ownerInput = screen.getByDisplayValue('user-123');
      await user.clear(ownerInput);

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/Project owner is required/)).toBeInTheDocument();
      });

      expect(vi.mocked(projectsApi.updateProject)).not.toHaveBeenCalled();
    });

    it('clears field error when user corrects the field', async () => {
      const user = userEvent.setup();

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const nameInput = screen.getByDisplayValue('Test Project');
      await user.clear(nameInput);

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/Project name is required/)).toBeInTheDocument();
      });

      await user.type(nameInput, 'Valid Name');

      await waitFor(() => {
        expect(screen.queryByText(/Project name is required/)).not.toBeInTheDocument();
      });
    });
  });

  describe('stale conflict', () => {
    it('shows reload prompt on concurrency conflict (409)', async () => {
      const user = userEvent.setup();
      const problemDetails = {
        type: 'ConflictError',
        status: 409,
        detail: 'The project was modified by another user',
      };
      vi.mocked(projectsApi.updateProject).mockRejectedValue(
        new ConflictError(problemDetails)
      );

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const nameInput = screen.getByDisplayValue('Test Project');
      await user.clear(nameInput);
      await user.type(nameInput, 'Updated Project');

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(
          screen.getByText(/Someone else changed this project/i)
        ).toBeInTheDocument();
      });

      const reloadButton = screen.getByRole('button', { name: /Reload/i });
      await user.click(reloadButton);

      expect(mockOnSaved).toHaveBeenCalled();
    });

    it('disables form during conflict state', async () => {
      const user = userEvent.setup();
      const problemDetails = { type: 'ConflictError', status: 409 };
      vi.mocked(projectsApi.updateProject).mockRejectedValue(
        new ConflictError(problemDetails)
      );

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const nameInput = screen.getByDisplayValue('Test Project');
      await user.clear(nameInput);
      await user.type(nameInput, 'Updated');

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/Someone else changed this project/i)).toBeInTheDocument();
      });

      // Form should be hidden, only conflict alert shown
      expect(screen.queryByDisplayValue('Updated')).not.toBeInTheDocument();
    });
  });

  describe('server validation errors', () => {
    it('displays server validation errors on relevant fields', async () => {
      const user = userEvent.setup();
      const validationErrors: Record<string, string[]> = {
        name: ['Project name must be unique'],
        ownerUserId: ['Owner must be an active user'],
      };
      vi.mocked(projectsApi.updateProject).mockRejectedValue(
        new ValidationError({ type: 'ValidationError', status: 400 }, validationErrors)
      );

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const nameInput = screen.getByDisplayValue('Test Project');
      await user.clear(nameInput);
      await user.type(nameInput, 'Duplicate Name');

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/Project name must be unique/)).toBeInTheDocument();
        expect(screen.getByText(/Owner must be an active user/)).toBeInTheDocument();
      });
    });

    it('shows general error message for validation errors', async () => {
      const user = userEvent.setup();
      const validationErrors = { name: ['Invalid name'] };
      vi.mocked(projectsApi.updateProject).mockRejectedValue(
        new ValidationError({ type: 'ValidationError', status: 400 }, validationErrors)
      );

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const nameInput = screen.getByDisplayValue('Test Project');
      await user.clear(nameInput);
      await user.type(nameInput, 'Bad Name');

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(
          screen.getByText(/Please fix the validation errors and try again/)
        ).toBeInTheDocument();
      });
    });
  });

  describe('general errors', () => {
    it('shows error message on API failure', async () => {
      const user = userEvent.setup();
      vi.mocked(projectsApi.updateProject).mockRejectedValue(
        new Error('Network error')
      );

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const nameInput = screen.getByDisplayValue('Test Project');
      await user.clear(nameInput);
      await user.type(nameInput, 'Updated Project');

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/Network error/)).toBeInTheDocument();
      });
    });
  });

  describe('cancel', () => {
    it('calls onCancel when Cancel button is clicked', async () => {
      const user = userEvent.setup();

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const cancelButton = screen.getByRole('button', { name: /Cancel/i });
      await user.click(cancelButton);

      expect(mockOnCancel).toHaveBeenCalled();
    });

    it('disables Cancel button while saving', async () => {
      const user = userEvent.setup();
      const mockResponse = {
        id: mockProject.id,
        clientId: mockProject.clientId,
        clientName: mockProject.clientName,
        name: 'Updated',
        description: mockProject.description || '',
        status: mockProject.status,
        priority: mockProject.priority,
        ownerUserId: mockProject.ownerUserId,
        startDateUtc: mockProject.startDateUtc,
        targetCompletionDateUtc: mockProject.targetCompletionDateUtc,
        actualCompletionDateUtc: mockProject.actualCompletionDateUtc,
        notes: mockProject.notes || '',
        createdAtUtc: mockProject.createdAtUtc,
        createdBy: mockProject.createdBy,
        lastModifiedAtUtc: mockProject.lastModifiedAtUtc,
        lastModifiedBy: mockProject.lastModifiedBy,
      };
      vi.mocked(projectsApi.updateProject).mockImplementation(
        () =>
          new Promise((resolve) => {
            setTimeout(() => resolve(mockResponse), 1000);
          })
      );

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const nameInput = screen.getByDisplayValue('Test Project');
      await user.clear(nameInput);
      await user.type(nameInput, 'Updated');

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      const cancelButton = screen.getByRole('button', { name: /Cancel/i });
      expect(cancelButton).toBeDisabled();
    });
  });

  describe('accessibility', () => {
    it('associates error messages with form fields', async () => {
      const user = userEvent.setup();

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const nameInput = screen.getByDisplayValue('Test Project');
      await user.clear(nameInput);

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        const errorElement = screen.getByText(/Project name is required/);
        expect(errorElement).toBeInTheDocument();
      });
    });

    it('marks invalid fields with aria-invalid', async () => {
      const user = userEvent.setup();
      const validationErrors = { name: ['Name is invalid'] };
      vi.mocked(projectsApi.updateProject).mockRejectedValue(
        new ValidationError({ type: 'ValidationError', status: 400 }, validationErrors)
      );

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const nameInput = screen.getByDisplayValue('Test Project');
      await user.clear(nameInput);
      await user.type(nameInput, 'Invalid');

      const saveButton = screen.getByRole('button', { name: /Save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        const fieldWithError = screen.getByDisplayValue('Invalid');
        expect(fieldWithError).toHaveAttribute('aria-invalid', 'true');
      });
    });

    it('has proper form labels for all inputs', () => {
      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      expect(screen.getByLabelText(/^Project name/)).toBeInTheDocument();
      expect(screen.getByLabelText(/^Priority/)).toBeInTheDocument();
      expect(screen.getByLabelText(/^Project owner/)).toBeInTheDocument();
      expect(screen.getByLabelText(/^Start date/)).toBeInTheDocument();
      expect(screen.getByLabelText(/^Target completion date/)).toBeInTheDocument();
      expect(screen.getByLabelText(/^Description/)).toBeInTheDocument();
      expect(screen.getByLabelText(/^Notes/)).toBeInTheDocument();
    });

    it('supports keyboard navigation through form fields', async () => {
      const user = userEvent.setup();

      render(
        <ProjectDetailsEditForm
          project={mockProject}
          onSaved={mockOnSaved}
          onCancel={mockOnCancel}
        />
      );

      const nameInput = screen.getByDisplayValue('Test Project');
      nameInput.focus();
      expect(nameInput).toHaveFocus();

      await user.tab();
      const prioritySelect = screen.getByDisplayValue('High');
      expect(prioritySelect).toHaveFocus();

      await user.tab();
      const ownerInput = screen.getByDisplayValue('user-123');
      expect(ownerInput).toHaveFocus();
    });
  });
});
