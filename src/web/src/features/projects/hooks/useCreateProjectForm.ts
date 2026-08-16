import { useCallback, useRef, useState } from 'react';
import { AuthenticationError, AuthorizationError, HttpError, ValidationError } from '@/api';
import { projectsApi } from '@/api/projects';
import type { CreateProjectRequest, Project } from '@/api/projects';

export interface ProjectFormValues {
  clientId: string;
  name: string;
  description: string;
}

export type ProjectFormField = keyof ProjectFormValues;
export type ProjectFormStatus = 'idle' | 'submitting' | 'success';

const REQUIRED_FIELD_MESSAGES: Record<'clientId' | 'name', string> = {
  clientId: 'Client is required.',
  name: 'Project name is required.',
};

const KNOWN_FIELDS: ProjectFormField[] = [
  'clientId',
  'name',
  'description',
];

export function createEmptyProjectFormValues(): ProjectFormValues {
  return {
    clientId: '',
    name: '',
    description: '',
  };
}

function normalizeErrorKey(key: string): string {
  return key.length > 0 ? key.charAt(0).toLowerCase() + key.slice(1) : key;
}

function mapServerFieldErrors(errors: Record<string, string[]>): Partial<Record<ProjectFormField, string>> {
  const mapped: Partial<Record<ProjectFormField, string>> = {};
  for (const [rawKey, messages] of Object.entries(errors)) {
    const normalized = normalizeErrorKey(rawKey) as ProjectFormField;
    if (KNOWN_FIELDS.includes(normalized) && messages.length > 0) {
      mapped[normalized] = messages[0];
    }
  }
  return mapped;
}

function validate(values: ProjectFormValues): Partial<Record<ProjectFormField, string>> {
  const errors: Partial<Record<ProjectFormField, string>> = {};
  if (values.clientId.trim().length === 0) {
    errors.clientId = REQUIRED_FIELD_MESSAGES.clientId;
  }
  if (values.name.trim().length === 0) {
    errors.name = REQUIRED_FIELD_MESSAGES.name;
  }
  return errors;
}

/**
 * Form state/submission for the Project create page (PROJECT-001, PROJECT-002).
 * Client-side validation guards required fields so required-field feedback is immediate;
 * every other validation rule is server-owned and mapped back onto the relevant field
 * from the 400 response (UX-003).
 */
export function useCreateProjectForm(ownerUserId: string) {
  const [values, setValues] = useState<ProjectFormValues>(createEmptyProjectFormValues);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<ProjectFormField, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [status, setStatus] = useState<ProjectFormStatus>('idle');
  const [createdProjectId, setCreatedProjectId] = useState<string | null>(null);
  const submittingRef = useRef(false);

  const setField = useCallback((field: ProjectFormField, value: string) => {
    setValues((prev) => ({ ...prev, [field]: value }));
    setFieldErrors((prev) => {
      if (!(field in prev)) {
        return prev;
      }
      const next = { ...prev };
      delete next[field];
      return next;
    });
  }, []);

  const submit = useCallback(async (): Promise<Project | null> => {
    if (submittingRef.current) {
      return null;
    }

    const validationErrors = validate(values);
    if (Object.keys(validationErrors).length > 0) {
      setFieldErrors(validationErrors);
      setFormError(null);
      return null;
    }

    submittingRef.current = true;
    setStatus('submitting');
    setFormError(null);
    setFieldErrors({});

    try {
      const trimmedDescription = values.description.trim();
      const request: CreateProjectRequest = {
        clientId: values.clientId.trim(),
        name: values.name.trim(),
        ownerUserId,
        ...(trimmedDescription && { description: trimmedDescription }),
      };

      const created = await projectsApi.createProject(request);
      setCreatedProjectId(created.id);
      setStatus('success');
      return created;
    } catch (err) {
      setStatus('idle');

      if (err instanceof ValidationError) {
        const mapped = mapServerFieldErrors(err.fieldErrors);
        setFieldErrors(mapped);
        if (Object.keys(mapped).length === 0) {
          setFormError(err.problemDetails.detail || 'The project could not be saved. Check the form and try again.');
        }
      } else if (err instanceof AuthenticationError) {
        setFormError('Your session has expired. Sign in again to create a project.');
      } else if (err instanceof AuthorizationError) {
        setFormError('You do not have permission to create projects.');
      } else if (err instanceof HttpError) {
        setFormError(err.problemDetails.detail || 'The project could not be saved. Try again.');
      } else {
        setFormError('The project could not be saved. Try again.');
      }

      return null;
    } finally {
      submittingRef.current = false;
    }
  }, [values, ownerUserId]);

  return {
    values,
    setField,
    fieldErrors,
    formError,
    status,
    createdProjectId,
    submit,
  };
}
