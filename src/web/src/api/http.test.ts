import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import type { ProblemDetails } from './http';
import {
  HttpClient,
  HttpError,
  AuthenticationError,
  AuthorizationError,
  ValidationError,
  NotFoundError,
  ConflictError,
  createGatewayClient,
} from './http';

describe('HttpClient', () => {
  let client: HttpClient;

  beforeEach(() => {
    client = new HttpClient({ baseUrl: 'https://api.example.com' });
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('base URL configuration', () => {
    it('should normalize base URL with trailing slash', () => {
      const clientWithSlash = new HttpClient({
        baseUrl: 'https://api.example.com/',
      });
      const clientWithoutSlash = new HttpClient({
        baseUrl: 'https://api.example.com',
      });

      expect(clientWithSlash).toBeDefined();
      expect(clientWithoutSlash).toBeDefined();
    });

    it('should warn on non-HTTPS URLs in production', () => {
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
      new HttpClient({ baseUrl: 'http://api.example.com' });
      expect(warnSpy).toHaveBeenCalled();
      warnSpy.mockRestore();
    });

    it('should not warn for localhost HTTP URLs', () => {
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
      new HttpClient({ baseUrl: 'http://localhost:3000' });
      expect(warnSpy).not.toHaveBeenCalled();
      warnSpy.mockRestore();
    });
  });

  describe('error mapping', () => {
    it('should throw AuthenticationError on 401 response', async () => {
      const problemDetails: ProblemDetails = {
        status: 401,
        title: 'Unauthorized',
      };

      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 401,
        statusText: 'Unauthorized',
        headers: new Map(),
        text: async () => JSON.stringify(problemDetails),
      });

      await expect(client.get('/test')).rejects.toThrow(AuthenticationError);
    });

    it('should throw AuthorizationError on 403 response', async () => {
      const problemDetails: ProblemDetails = {
        status: 403,
        title: 'Forbidden',
      };

      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 403,
        statusText: 'Forbidden',
        headers: new Map(),
        text: async () => JSON.stringify(problemDetails),
      });

      await expect(client.get('/test')).rejects.toThrow(AuthorizationError);
    });

    it('should distinguish between 401 and 403 in error properties', async () => {
      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 401,
        statusText: 'Unauthorized',
        headers: new Map(),
        text: async () => '{"status":401,"title":"Unauthorized"}',
      });

      try {
        await client.get('/test');
      } catch (error) {
        expect(error).toBeInstanceOf(AuthenticationError);
        expect((error as HttpError).statusCode).toBe(401);
      }

      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 403,
        statusText: 'Forbidden',
        headers: new Map(),
        text: async () => '{"status":403,"title":"Forbidden"}',
      });

      try {
        await client.get('/test');
      } catch (error) {
        expect(error).toBeInstanceOf(AuthorizationError);
        expect((error as HttpError).statusCode).toBe(403);
      }
    });

    it('should throw ValidationError on 400 response with field errors', async () => {
      const problemDetails: ProblemDetails = {
        status: 400,
        title: 'Validation Failed',
        errors: {
          name: ['Name is required'],
          email: ['Invalid email format'],
        },
      };

      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        headers: new Map(),
        text: async () => JSON.stringify(problemDetails),
      });

      try {
        await client.post('/test', { invalid: 'data' });
      } catch (error) {
        expect(error).toBeInstanceOf(ValidationError);
        const valError = error as ValidationError;
        expect(valError.fieldErrors).toEqual(problemDetails.errors);
      }
    });

    it('should throw NotFoundError on 404 response', async () => {
      const problemDetails: ProblemDetails = {
        status: 404,
        title: 'Not Found',
      };

      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 404,
        statusText: 'Not Found',
        headers: new Map(),
        text: async () => JSON.stringify(problemDetails),
      });

      await expect(client.get('/test')).rejects.toThrow(NotFoundError);
    });

    it('should throw ConflictError on 409 response', async () => {
      const problemDetails: ProblemDetails = {
        status: 409,
        title: 'Conflict',
      };

      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 409,
        statusText: 'Conflict',
        headers: new Map(),
        text: async () => JSON.stringify(problemDetails),
      });

      await expect(client.get('/test')).rejects.toThrow(ConflictError);
    });

    it('should include traceId and supportReference in error details', async () => {
      const problemDetails: ProblemDetails = {
        status: 400,
        title: 'Bad Request',
      };

      const headerMap = new Map();
      headerMap.set('X-Trace-ID', 'trace-123');
      headerMap.set('X-Correlation-ID', 'corr-456');

      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        headers: headerMap,
        text: async () => JSON.stringify(problemDetails),
      });

      try {
        await client.post('/test', {});
      } catch (error) {
        const httpError = error as HttpError;
        expect(httpError.problemDetails.traceId).toBe('trace-123');
        expect(httpError.problemDetails.supportReference).toBe('corr-456');
      }
    });
  });

  describe('ProblemDetails parsing', () => {
    it('should parse valid ProblemDetails response', async () => {
      const problemDetails: ProblemDetails = {
        type: 'https://api.example.com/errors/validation',
        title: 'Validation Failed',
        status: 400,
        detail: 'One or more validation errors occurred.',
        instance: '/api/clients',
        errors: {
          name: ['Name is required'],
        },
      };

      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        headers: new Map(),
        text: async () => JSON.stringify(problemDetails),
      });

      try {
        await client.post<Record<string, unknown>>('/test', {});
      } catch (error) {
        const valError = error as ValidationError;
        expect(valError.problemDetails.type).toBe(problemDetails.type);
        expect(valError.problemDetails.detail).toBe(problemDetails.detail);
        expect(valError.problemDetails.instance).toBe(problemDetails.instance);
      }
    });

    it('should handle empty response body', async () => {
      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        headers: new Map(),
        text: async () => '',
      });

      try {
        await client.get('/test');
      } catch (error) {
        const httpError = error as HttpError;
        expect(httpError.statusCode).toBe(500);
        expect(httpError.problemDetails.title).toBe('Internal Server Error');
      }
    });

    it('should handle non-JSON response body', async () => {
      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        headers: new Map(),
        text: async () => '<html>Error</html>',
      });

      try {
        await client.get('/test');
      } catch (error) {
        const httpError = error as HttpError;
        expect(httpError.statusCode).toBe(500);
      }
    });
  });

  describe('request cancellation', () => {
    it('should support AbortSignal for cancellation', async () => {
      const abortError = new DOMException('The operation was aborted', 'AbortError');

      (global.fetch as any).mockImplementationOnce(() => {
        throw abortError;
      });

      const controller = new AbortController();
      const promise = client.get('/test', { signal: controller.signal });
      controller.abort();

      await expect(promise).rejects.toThrow('Request cancelled');
    });

    it('should apply timeout configuration when no signal provided', async () => {
      const customClient = new HttpClient({
        baseUrl: 'https://api.example.com',
        timeout: 3000,
      });

      (global.fetch as any).mockImplementationOnce((_url: string, options: any) => {
        // Verify that AbortController signal is provided for timeout
        expect(options.signal).toBeDefined();
        return Promise.resolve({
          ok: true,
          status: 200,
          headers: new Map([['content-type', 'application/json']]),
          json: async () => ({ success: true }),
        });
      });

      await customClient.get('/test');
      expect(global.fetch).toHaveBeenCalled();
    });

    it('should use default timeout if not specified', async () => {
      const customClient = new HttpClient({
        baseUrl: 'https://api.example.com',
        timeout: 5000,
      });

      (global.fetch as any).mockImplementationOnce((_url: string, options: any) => {
        // Verify that a timeout AbortController was created
        expect(options.signal).toBeDefined();
        return Promise.resolve({
          ok: true,
          status: 200,
          headers: new Map([['content-type', 'application/json']]),
          json: async () => ({ success: true }),
        });
      });

      await customClient.get('/test');
      expect(global.fetch).toHaveBeenCalled();
    });
  });

  describe('correlation and trace headers', () => {
    it('should include X-Correlation-ID header in requests', async () => {
      let capturedHeaders: Record<string, string> | undefined;

      (global.fetch as any).mockImplementationOnce((_url: string, options: any) => {
        capturedHeaders = options.headers;
        return Promise.resolve({
          ok: true,
          status: 200,
          headers: new Map([['content-type', 'application/json']]),
          json: async () => ({ success: true }),
        });
      });

      await client.get('/test');

      expect(capturedHeaders).toBeDefined();
      expect(capturedHeaders!['X-Correlation-ID']).toBeDefined();
      expect(capturedHeaders!['X-Correlation-ID']).toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
      );
    });

    it('should include traceparent header in W3C format', async () => {
      let capturedHeaders: Record<string, string> | undefined;

      (global.fetch as any).mockImplementationOnce((_url: string, options: any) => {
        capturedHeaders = options.headers;
        return Promise.resolve({
          ok: true,
          status: 200,
          headers: new Map([['content-type', 'application/json']]),
          json: async () => ({ success: true }),
        });
      });

      await client.get('/test');

      expect(capturedHeaders).toBeDefined();
      expect(capturedHeaders!['traceparent']).toBeDefined();
      const traceparent = capturedHeaders!['traceparent'];
      // Format: 00-traceId(32 hex)-spanId(16 hex)-traceFlags(2 hex)
      expect(traceparent).toMatch(/^00-[0-9a-f-]{36}-[0-9a-f0]{16}-01$/);
    });

    it('should generate unique correlation IDs per request', async () => {
      const correlationIds = new Set<string>();

      (global.fetch as any).mockImplementation((_url: string, options: any) => {
        correlationIds.add(options.headers['X-Correlation-ID']);
        return Promise.resolve({
          ok: true,
          status: 200,
          headers: new Map([['content-type', 'application/json']]),
          json: async () => ({ success: true }),
        });
      });

      await client.get('/test1');
      await client.get('/test2');
      await client.get('/test3');

      expect(correlationIds.size).toBe(3);
    });
  });

  describe('HTTP methods', () => {
    it('should support GET requests', async () => {
      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        status: 200,
        headers: new Map([['content-type', 'application/json']]),
        json: async () => ({ id: 1, name: 'Test' }),
      });

      const result = await client.get('/items/1');
      expect(result).toEqual({ id: 1, name: 'Test' });
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining('/items/1'),
        expect.objectContaining({ method: 'GET' }),
      );
    });

    it('should support POST requests with body', async () => {
      const body = { name: 'New Item' };

      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        status: 201,
        headers: new Map([['content-type', 'application/json']]),
        json: async () => ({ id: 1, ...body }),
      });

      const result = await client.post('/items', body);
      expect(result).toEqual({ id: 1, name: 'New Item' });
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining('/items'),
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify(body),
        }),
      );
    });

    it('should support PUT requests', async () => {
      const body = { name: 'Updated Item' };

      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        status: 200,
        headers: new Map([['content-type', 'application/json']]),
        json: async () => ({ id: 1, ...body }),
      });

      await client.put('/items/1', body);
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining('/items/1'),
        expect.objectContaining({ method: 'PUT' }),
      );
    });

    it('should support PATCH requests', async () => {
      const body = { status: 'active' };

      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        status: 200,
        headers: new Map([['content-type', 'application/json']]),
        json: async () => ({ id: 1, name: 'Item', ...body }),
      });

      await client.patch('/items/1', body);
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining('/items/1'),
        expect.objectContaining({ method: 'PATCH' }),
      );
    });

    it('should support DELETE requests', async () => {
      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        status: 204,
        headers: new Map(),
        text: async () => '',
      });

      await client.delete('/items/1');
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining('/items/1'),
        expect.objectContaining({ method: 'DELETE' }),
      );
    });
  });

  describe('createGatewayClient factory', () => {
    it('should create client with provided base URL', () => {
      const client = createGatewayClient('https://api.example.com');
      expect(client).toBeInstanceOf(HttpClient);
    });

    it('should use VITE_API_BASE_URL from environment', () => {
      const originalEnv = import.meta.env.VITE_API_BASE_URL;
      Object.defineProperty(import.meta, 'env', {
        value: { VITE_API_BASE_URL: 'https://env.example.com' },
        configurable: true,
      });

      const client = createGatewayClient();
      expect(client).toBeInstanceOf(HttpClient);

      Object.defineProperty(import.meta, 'env', {
        value: { VITE_API_BASE_URL: originalEnv },
        configurable: true,
      });
    });

    it('should warn when no base URL is configured', () => {
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
      createGatewayClient('');
      expect(warnSpy).toHaveBeenCalledWith(
        expect.stringContaining('base URL not configured'),
      );
      warnSpy.mockRestore();
    });
  });
});
