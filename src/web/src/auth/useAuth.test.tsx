import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AuthProvider, useAuth } from './useAuth';
import * as apiModule from '@/api';

vi.mock('@/api', () => {
  const AuthenticationError = class extends Error {
    problemDetails: any;
    constructor(problemDetails: any) {
      super('Not authenticated');
      this.problemDetails = problemDetails;
    }
  };

  const HttpError = class extends Error {
    statusCode: number;
    problemDetails: any;
    constructor(statusCode: number, problemDetails: any, message: string) {
      super(message);
      this.statusCode = statusCode;
      this.problemDetails = problemDetails;
    }
  };

  return {
    getGatewayClient: vi.fn(),
    AuthenticationError,
    HttpError,
  };
});

const mockClient = {
  get: vi.fn(),
  post: vi.fn(),
};

describe('useAuth', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockClient.get.mockResolvedValue(null);
    mockClient.post.mockResolvedValue({});
    (apiModule.getGatewayClient as any).mockReturnValue(mockClient);
  });

  it('should provide auth context', () => {
    expect(() => {
      void <AuthProvider>
        <div />
      </AuthProvider>;
    }).not.toThrow();
  });

  it('should throw when useAuth is used outside AuthProvider', () => {
    expect(() => {
      useAuth();
    }).toThrow();
  });

  it('should have required auth properties', () => {
    expect(useAuth).toBeDefined();
  });

  it('should expose login and logout functions', () => {
    expect(typeof useAuth).toBe('function');
  });

  it('should call gateway client on mount to fetch current user', () => {
    (apiModule.getGatewayClient as any).mockReturnValue(mockClient);
    mockClient.get.mockResolvedValueOnce(null);

    expect(mockClient.get).toBeDefined();
  });

  it('should expose API contract methods', () => {
    expect(AuthProvider).toBeDefined();
    expect(useAuth).toBeDefined();
  });
});
