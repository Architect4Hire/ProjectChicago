import { useCallback, useRef, useState } from 'react';
import { AuthenticationError, AuthorizationError, HttpError, ValidationError } from '@/api';
import { clientsApi } from '@/api/clients';
import type { ClientDuplicateWarning, CreateClientRequest } from '@/api/clients';

export interface ClientFormValues {
  name: string;
  ownerUserId: string;
  primaryContactName: string;
  primaryEmail: string;
  primaryPhone: string;
  website: string;
  addressLine: string;
  city: string;
  stateOrProvince: string;
  postalCode: string;
  country: string;
  description: string;
}

export type ClientFormField = keyof ClientFormValues;
export type ClientFormStatus = 'idle' | 'submitting' | 'success';

export interface ClientCreateResult {
  id: string;
  hasDuplicates: boolean;
}

const REQUIRED_FIELD_MESSAGES: Record<'name' | 'ownerUserId', string> = {
  name: 'Client name is required.',
  ownerUserId: 'Assigned owner is required.',
};

const KNOWN_FIELDS: ClientFormField[] = [
  'name',
  'ownerUserId',
  'primaryContactName',
  'primaryEmail',
  'primaryPhone',
  'website',
  'addressLine',
  'city',
  'stateOrProvince',
  'postalCode',
  'country',
  'description',
];

export function createEmptyClientFormValues(defaultOwnerUserId = ''): ClientFormValues {
  return {
    name: '',
    ownerUserId: defaultOwnerUserId,
    primaryContactName: '',
    primaryEmail: '',
    primaryPhone: '',
    website: '',
    addressLine: '',
    city: '',
    stateOrProvince: '',
    postalCode: '',
    country: '',
    description: '',
  };
}

function optional(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
}

// CreateClientViewModel requires only name/ownerUserId; every other field is optional on the
// wire, so blank inputs must be omitted rather than sent as empty strings (an empty string would
// fail the backend's [EmailAddress]/[Url] format validation instead of being treated as "not
// provided").
function buildRequest(values: ClientFormValues): CreateClientRequest {
  return {
    name: values.name.trim(),
    ownerUserId: values.ownerUserId.trim(),
    primaryContactName: optional(values.primaryContactName),
    primaryEmail: optional(values.primaryEmail),
    primaryPhone: optional(values.primaryPhone),
    website: optional(values.website),
    addressLine: optional(values.addressLine),
    city: optional(values.city),
    stateOrProvince: optional(values.stateOrProvince),
    postalCode: optional(values.postalCode),
    country: optional(values.country),
    description: optional(values.description),
  };
}

// The backend's automatic [ApiController] model-validation 400 keys `errors` by the C# property
// name (PascalCase); manually-built validation responses elsewhere in the API use the same
// convention. Normalize defensively so either PascalCase or camelCase keys resolve to a field.
function normalizeErrorKey(key: string): string {
  return key.length > 0 ? key.charAt(0).toLowerCase() + key.slice(1) : key;
}

function mapServerFieldErrors(errors: Record<string, string[]>): Partial<Record<ClientFormField, string>> {
  const mapped: Partial<Record<ClientFormField, string>> = {};
  for (const [rawKey, messages] of Object.entries(errors)) {
    const normalized = normalizeErrorKey(rawKey) as ClientFormField;
    if (KNOWN_FIELDS.includes(normalized) && messages.length > 0) {
      mapped[normalized] = messages[0];
    }
  }
  return mapped;
}

function validate(values: ClientFormValues): Partial<Record<ClientFormField, string>> {
  const errors: Partial<Record<ClientFormField, string>> = {};
  if (values.name.trim().length === 0) {
    errors.name = REQUIRED_FIELD_MESSAGES.name;
  }
  if (values.ownerUserId.trim().length === 0) {
    errors.ownerUserId = REQUIRED_FIELD_MESSAGES.ownerUserId;
  }
  return errors;
}

/**
 * Form state/submission for the Client create page (CLIENT-001, CLIENT-002).
 * Client-side validation only guards the two backend-required fields (name, ownerUserId) so
 * required-field feedback is immediate; every other validation rule is server-owned and mapped
 * back onto the relevant field from the 400 response (UX-003, security.md: "Normalize and
 * validate ... server-side").
 */
export function useCreateClientForm(defaultOwnerUserId = '') {
  const [values, setValues] = useState<ClientFormValues>(() => createEmptyClientFormValues(defaultOwnerUserId));
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<ClientFormField, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [status, setStatus] = useState<ClientFormStatus>('idle');
  const [duplicates, setDuplicates] = useState<ClientDuplicateWarning[]>([]);
  const [createdClientId, setCreatedClientId] = useState<string | null>(null);
  const submittingRef = useRef(false);

  const setField = useCallback((field: ClientFormField, value: string) => {
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

  const submit = useCallback(async (): Promise<ClientCreateResult | null> => {
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
      const created = await clientsApi.createClient(buildRequest(values));
      const possibleDuplicates = created.possibleDuplicates ?? [];
      setDuplicates(possibleDuplicates);
      setCreatedClientId(created.id);
      setStatus('success');
      return { id: created.id, hasDuplicates: possibleDuplicates.length > 0 };
    } catch (err) {
      setStatus('idle');

      if (err instanceof ValidationError) {
        const mapped = mapServerFieldErrors(err.fieldErrors);
        setFieldErrors(mapped);
        if (Object.keys(mapped).length === 0) {
          setFormError(err.problemDetails.detail || 'The client could not be saved. Check the form and try again.');
        }
      } else if (err instanceof AuthenticationError) {
        setFormError('Your session has expired. Sign in again to create a client.');
      } else if (err instanceof AuthorizationError) {
        setFormError('You do not have permission to create clients.');
      } else if (err instanceof HttpError) {
        setFormError(err.problemDetails.detail || 'The client could not be saved. Try again.');
      } else {
        setFormError('The client could not be saved. Try again.');
      }

      return null;
    } finally {
      submittingRef.current = false;
    }
  }, [values]);

  return {
    values,
    setField,
    fieldErrors,
    formError,
    status,
    duplicates,
    createdClientId,
    submit,
  };
}
