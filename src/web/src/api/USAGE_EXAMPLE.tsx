import { useEffect, useState } from 'react';
import {
  getGatewayClient,
  AuthenticationError,
  AuthorizationError,
  ValidationError,
  NotFoundError,
  HttpError,
} from './index';

interface Client {
  id: string;
  name: string;
  email: string;
}

export function ClientDetailExample({ clientId }: { clientId: string }) {
  const [client, setClient] = useState<Client | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [traceId, setTraceId] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();

    const fetchClient = async () => {
      try {
        setLoading(true);
        const httpClient = getGatewayClient();
        const data = await httpClient.get<Client>(
          `/api/clients/${clientId}`,
          { signal: controller.signal },
        );
        setClient(data);
        setError(null);
      } catch (err) {
        if (err instanceof AuthenticationError) {
          setError('You are not authenticated. Please log in.');
        } else if (err instanceof AuthorizationError) {
          setError('You do not have permission to view this client.');
        } else if (err instanceof NotFoundError) {
          setError('Client not found.');
        } else if (err instanceof HttpError) {
          const supportRef = err.problemDetails.supportReference;
          setError(
            `Error loading client${supportRef ? `. Support reference: ${supportRef}` : '.'}`,
          );
          setTraceId(err.problemDetails.traceId || null);
        } else {
          setError('An unexpected error occurred.');
        }
      } finally {
        setLoading(false);
      }
    };

    fetchClient();

    return () => controller.abort();
  }, [clientId]);

  if (loading) {
    return <div>Loading...</div>;
  }

  if (error) {
    return (
      <div role="alert">
        <p>{error}</p>
        {traceId && <p style={{ fontSize: '0.875em', color: '#666' }}>Trace ID: {traceId}</p>}
      </div>
    );
  }

  if (!client) {
    return <div>No client data</div>;
  }

  return (
    <div>
      <h1>{client.name}</h1>
      <p>Email: {client.email}</p>
    </div>
  );
}

export function CreateClientExample() {
  const [formError, setFormError] = useState<Record<string, string[]> | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setLoading(true);
    setFormError(null);

    try {
      const formData = new FormData(e.currentTarget);
      const httpClient = getGatewayClient();

      const result = await httpClient.post<Client>('/api/clients', {
        name: formData.get('name'),
        email: formData.get('email'),
      });

      console.log('Client created:', result);
      e.currentTarget.reset();
    } catch (err) {
      if (err instanceof ValidationError) {
        setFormError(err.fieldErrors);
      } else if (err instanceof HttpError) {
        const supportRef = err.problemDetails.supportReference;
        setFormError({
          _form: [`Failed to create client${supportRef ? ` (${supportRef})` : ''}`],
        });
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit}>
      <div>
        <label htmlFor="name">Client Name</label>
        <input id="name" name="name" type="text" required />
        {formError?.name && <p style={{ color: 'red' }}>{formError.name[0]}</p>}
      </div>

      <div>
        <label htmlFor="email">Email</label>
        <input id="email" name="email" type="email" required />
        {formError?.email && <p style={{ color: 'red' }}>{formError.email[0]}</p>}
      </div>

      {formError?._form && <p style={{ color: 'red' }}>{formError._form[0]}</p>}

      <button type="submit" disabled={loading}>
        {loading ? 'Creating...' : 'Create Client'}
      </button>
    </form>
  );
}
