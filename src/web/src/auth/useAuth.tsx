import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react';
import { getGatewayClient, AuthenticationError, HttpError, setCsrfToken } from '@/api';

export interface CurrentUser {
  userId: string;
  email: string;
  userName: string;
  roles: string[];
}

export interface AuthContextType {
  currentUser: CurrentUser | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  error: string | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const client = getGatewayClient();

  const refreshUser = useCallback(async () => {
    try {
      setError(null);
      const user = await client.get<CurrentUser>('/auth/current-user');
      setCurrentUser(user);
    } catch (err) {
      if (err instanceof AuthenticationError) {
        setCurrentUser(null);
      } else if (err instanceof HttpError) {
        setError(`Failed to load user: ${err.problemDetails.detail || err.message}`);
      }
    }
  }, [client]);

  const login = useCallback(
    async (email: string, password: string) => {
      try {
        setError(null);
        setIsLoading(true);

        const response = await client.post<{ user: CurrentUser }>('/auth/login', {
          Email: email,
          Password: password,
        });

        setCurrentUser(response.user);
      } catch (err) {
        if (err instanceof AuthenticationError) {
          setError('Invalid email or password');
        } else if (err instanceof HttpError) {
          const detail = (err.problemDetails as unknown as { detail?: string })?.detail;
          setError(detail || err.message || 'Login failed');
        } else {
          setError(err instanceof Error ? err.message : 'Login failed');
        }
        throw err;
      } finally {
        setIsLoading(false);
      }
    },
    [client],
  );

  const logout = useCallback(async () => {
    try {
      setError(null);
      await client.post('/auth/logout', {});
    } catch (err) {
      // Log out locally even if the server request fails
      console.error('Logout error:', err);
    } finally {
      setCurrentUser(null);
      setCsrfToken(''); // Clear CSRF token on logout (ADR-0018-superseding BFF).
    }
  }, [client]);

  // Load current user on mount
  useEffect(() => {
    const initAuth = async () => {
      setIsLoading(true);
      await refreshUser();
      setIsLoading(false);
    };

    initAuth();
  }, [refreshUser]);

  return (
    <AuthContext.Provider
      value={{
        currentUser,
        isLoading,
        isAuthenticated: currentUser !== null,
        error,
        login,
        logout,
        refreshUser,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextType {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
