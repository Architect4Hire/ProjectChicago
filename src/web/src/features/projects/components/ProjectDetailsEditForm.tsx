import { useState, useCallback, type FC } from 'react';
import { Button, Field, Input, Select, Stack } from '@/design-system';
import { projectsApi } from '@/api/projects';
import type { ProjectDetailRecord, UpdateProjectRequest } from '@/api/projects';
import { ConflictError, ValidationError } from '@/api';

interface ProjectDetailsEditFormProps {
  project: ProjectDetailRecord;
  onSaved: () => void;
  onCancel: () => void;
}

export const ProjectDetailsEditForm: FC<ProjectDetailsEditFormProps> = ({
  project,
  onSaved,
  onCancel,
}) => {
  const [name, setName] = useState(project.name);
  const [description, setDescription] = useState(project.description || '');
  const [priority, setPriority] = useState(project.priority);
  const [ownerUserId, setOwnerUserId] = useState(project.ownerUserId);
  const [startDateUtc, setStartDateUtc] = useState(
    project.startDateUtc ? project.startDateUtc.split('T')[0] : ''
  );
  const [targetCompletionDateUtc, setTargetCompletionDateUtc] = useState(
    project.targetCompletionDateUtc ? project.targetCompletionDateUtc.split('T')[0] : ''
  );
  const [notes, setNotes] = useState(project.notes || '');

  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [conflict, setConflict] = useState(false);
  const [success, setSuccess] = useState(false);

  const validateDates = useCallback((): boolean => {
    const errors: Record<string, string> = {};
    let isValid = true;

    if (startDateUtc && targetCompletionDateUtc) {
      const start = new Date(startDateUtc);
      const target = new Date(targetCompletionDateUtc);
      if (start > target) {
        errors.startDateUtc = 'Start date must be before target completion date';
        isValid = false;
      }
    }

    if (startDateUtc) {
      const start = new Date(startDateUtc);
      if (isNaN(start.getTime())) {
        errors.startDateUtc = 'Invalid date format';
        isValid = false;
      }
    }

    if (targetCompletionDateUtc) {
      const target = new Date(targetCompletionDateUtc);
      if (isNaN(target.getTime())) {
        errors.targetCompletionDateUtc = 'Invalid date format';
        isValid = false;
      }
    }

    setFieldErrors(errors);
    return isValid;
  }, [startDateUtc, targetCompletionDateUtc]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validateDates()) {
      setError(null);
      return;
    }

    if (!name.trim()) {
      setFieldErrors({ name: 'Project name is required' });
      setError(null);
      return;
    }

    if (!ownerUserId.trim()) {
      setFieldErrors({ ownerUserId: 'Project owner is required' });
      setError(null);
      return;
    }

    setIsSaving(true);
    setError(null);
    setFieldErrors({});
    setConflict(false);
    setSuccess(false);

    try {
      const payload: UpdateProjectRequest = {};

      if (name !== project.name) payload.name = name;
      if (description !== (project.description || '')) payload.description = description || undefined;
      if (priority !== project.priority) payload.priority = priority;
      if (ownerUserId !== project.ownerUserId) payload.ownerUserId = ownerUserId;

      const startDate = startDateUtc
        ? new Date(startDateUtc + 'T00:00:00Z').toISOString()
        : undefined;
      if (startDate !== project.startDateUtc) payload.startDateUtc = startDate;

      const targetDate = targetCompletionDateUtc
        ? new Date(targetCompletionDateUtc + 'T00:00:00Z').toISOString()
        : undefined;
      if (targetDate !== project.targetCompletionDateUtc) payload.targetCompletionDateUtc = targetDate;

      if (notes !== (project.notes || '')) payload.notes = notes || undefined;

      // Only send request if there are actual changes
      if (Object.keys(payload).length === 0) {
        setSuccess(true);
        setTimeout(() => {
          onSaved();
        }, 1500);
        return;
      }

      await projectsApi.updateProject(project.id, payload);

      setSuccess(true);
      setTimeout(() => {
        onSaved();
      }, 1500);
    } catch (err) {
      setSuccess(false);

      if (err instanceof ConflictError) {
        setConflict(true);
      } else if (err instanceof ValidationError) {
        const errors: Record<string, string> = {};
        Object.entries(err.fieldErrors).forEach(([key, messages]) => {
          errors[key] = messages[0] || 'Invalid value';
        });
        setFieldErrors(errors);
        setError('Please fix the validation errors and try again.');
      } else {
        const message = err instanceof Error ? err.message : 'Failed to save project details';
        setError(message);
      }
    } finally {
      setIsSaving(false);
    }
  };

  const handleReload = () => {
    setConflict(false);
    onSaved();
  };

  if (success) {
    return (
      <div
        role="status"
        className="flex items-center gap-2 rounded-lg border border-success-300 bg-success-50 px-4 py-3 text-sm text-success-700 dark:border-success-800 dark:bg-success-900/20 dark:text-success-400"
      >
        <svg
          className="size-5 flex-shrink-0"
          fill="currentColor"
          viewBox="0 0 20 20"
          aria-hidden="true"
        >
          <path
            fillRule="evenodd"
            d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
            clipRule="evenodd"
          />
        </svg>
        <span>Project details saved successfully.</span>
      </div>
    );
  }

  if (conflict) {
    return (
      <div
        role="alert"
        className="flex flex-wrap items-center gap-2 rounded-lg border border-warning-300 bg-warning-50 px-3 py-2 text-sm text-warning-700 dark:border-warning-800 dark:bg-warning-900/20 dark:text-warning-400"
      >
        <span>Someone else changed this project. Reload to see the latest details before trying again.</span>
        <Button type="button" variant="ghost" size="sm" onClick={handleReload}>
          Reload
        </Button>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit}>
      <Stack className="gap-6">
        {error && (
          <div
            role="alert"
            className="flex items-start gap-2 rounded-lg border border-error-300 bg-error-50 px-4 py-3 text-sm text-error-700 dark:border-error-800 dark:bg-error-900/20 dark:text-error-400"
          >
            <svg
              className="mt-0.5 size-5 flex-shrink-0"
              fill="currentColor"
              viewBox="0 0 20 20"
              aria-hidden="true"
            >
              <path
                fillRule="evenodd"
                d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
                clipRule="evenodd"
              />
            </svg>
            <span>{error}</span>
          </div>
        )}

        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
          <Field label="Project name" required error={fieldErrors.name} hint="Give the project a clear, descriptive name">
            <Input
              type="text"
              value={name}
              onChange={(e) => {
                setName(e.target.value);
                if (fieldErrors.name) {
                  const newErrors = { ...fieldErrors };
                  delete newErrors.name;
                  setFieldErrors(newErrors);
                }
              }}
              disabled={isSaving}
              invalid={Boolean(fieldErrors.name)}
              placeholder="Project name"
              aria-describedby={fieldErrors.name ? 'name-error' : undefined}
            />
          </Field>

          <Field label="Priority" required error={fieldErrors.priority}>
            <Select
              value={priority}
              onChange={(e) => {
                setPriority(e.target.value as any);
                if (fieldErrors.priority) {
                  const newErrors = { ...fieldErrors };
                  delete newErrors.priority;
                  setFieldErrors(newErrors);
                }
              }}
              disabled={isSaving}
              invalid={Boolean(fieldErrors.priority)}
              aria-describedby={fieldErrors.priority ? 'priority-error' : undefined}
            >
              <option value="Low">Low</option>
              <option value="Normal">Normal</option>
              <option value="High">High</option>
              <option value="Critical">Critical</option>
            </Select>
          </Field>

          <Field label="Project owner" required error={fieldErrors.ownerUserId}>
            <Input
              type="text"
              value={ownerUserId}
              onChange={(e) => {
                setOwnerUserId(e.target.value);
                if (fieldErrors.ownerUserId) {
                  const newErrors = { ...fieldErrors };
                  delete newErrors.ownerUserId;
                  setFieldErrors(newErrors);
                }
              }}
              disabled={isSaving}
              invalid={Boolean(fieldErrors.ownerUserId)}
              placeholder="Owner user ID"
              aria-describedby={fieldErrors.ownerUserId ? 'ownerUserId-error' : undefined}
            />
          </Field>

          <Field label="Start date" error={fieldErrors.startDateUtc} hint="Project start date (UTC)">
            <Input
              type="date"
              value={startDateUtc}
              onChange={(e) => {
                setStartDateUtc(e.target.value);
                if (fieldErrors.startDateUtc) {
                  const newErrors = { ...fieldErrors };
                  delete newErrors.startDateUtc;
                  setFieldErrors(newErrors);
                }
              }}
              disabled={isSaving}
              invalid={Boolean(fieldErrors.startDateUtc)}
              aria-describedby={fieldErrors.startDateUtc ? 'startDateUtc-error' : undefined}
            />
          </Field>

          <Field
            label="Target completion date"
            error={fieldErrors.targetCompletionDateUtc}
            hint="Expected completion date (UTC)"
          >
            <Input
              type="date"
              value={targetCompletionDateUtc}
              onChange={(e) => {
                setTargetCompletionDateUtc(e.target.value);
                if (fieldErrors.targetCompletionDateUtc) {
                  const newErrors = { ...fieldErrors };
                  delete newErrors.targetCompletionDateUtc;
                  setFieldErrors(newErrors);
                }
              }}
              disabled={isSaving}
              invalid={Boolean(fieldErrors.targetCompletionDateUtc)}
              aria-describedby={
                fieldErrors.targetCompletionDateUtc ? 'targetCompletionDateUtc-error' : undefined
              }
            />
          </Field>
        </div>

        <Field label="Description" error={fieldErrors.description}>
          <textarea
            value={description}
            onChange={(e) => {
              setDescription(e.target.value);
              if (fieldErrors.description) {
                const newErrors = { ...fieldErrors };
                delete newErrors.description;
                setFieldErrors(newErrors);
              }
            }}
            disabled={isSaving}
            placeholder="Describe the project scope, goals, and deliverables"
            className="w-full rounded-lg border border-gray-300 bg-white px-3.5 py-2.5 text-sm text-gray-900 shadow-theme-xs transition-colors placeholder:text-gray-400 disabled:cursor-not-allowed disabled:bg-gray-100 disabled:text-gray-500 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90 dark:disabled:bg-gray-800 outline-none focus-visible:ring-4 focus-visible:ring-brand-500/15 focus-visible:border-brand-500"
            rows={4}
            aria-invalid={Boolean(fieldErrors.description) || undefined}
            aria-describedby={fieldErrors.description ? 'description-error' : undefined}
          />
        </Field>

        <Field label="Notes" error={fieldErrors.notes}>
          <textarea
            value={notes}
            onChange={(e) => {
              setNotes(e.target.value);
              if (fieldErrors.notes) {
                const newErrors = { ...fieldErrors };
                delete newErrors.notes;
                setFieldErrors(newErrors);
              }
            }}
            disabled={isSaving}
            placeholder="Additional notes or comments about the project"
            className="w-full rounded-lg border border-gray-300 bg-white px-3.5 py-2.5 text-sm text-gray-900 shadow-theme-xs transition-colors placeholder:text-gray-400 disabled:cursor-not-allowed disabled:bg-gray-100 disabled:text-gray-500 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90 dark:disabled:bg-gray-800 outline-none focus-visible:ring-4 focus-visible:ring-brand-500/15 focus-visible:border-brand-500"
            rows={3}
            aria-invalid={Boolean(fieldErrors.notes) || undefined}
            aria-describedby={fieldErrors.notes ? 'notes-error' : undefined}
          />
        </Field>

        <div className="flex flex-wrap gap-2 border-t border-gray-200 pt-6 dark:border-gray-800">
          <Button type="submit" disabled={isSaving} isLoading={isSaving}>
            {isSaving ? 'Saving...' : 'Save changes'}
          </Button>
          <Button
            type="button"
            variant="outline"
            onClick={onCancel}
            disabled={isSaving}
          >
            Cancel
          </Button>
        </div>
      </Stack>
    </form>
  );
};
