import { useState, useEffect } from 'react';
import { clientsApi } from '@/api/clients';
import type { Client } from '@/api/clients';

export function useAvailableClients() {
  const [clients, setClients] = useState<Client[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchClients = async () => {
      try {
        setIsLoading(true);
        setError(null);
        const result = await clientsApi.listClients({
          pageSize: 1000,
          excludeArchived: true,
        });
        setClients(result.items || []);
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Failed to load clients';
        setError(message);
      } finally {
        setIsLoading(false);
      }
    };

    fetchClients();
  }, []);

  return { clients, isLoading, error };
}
