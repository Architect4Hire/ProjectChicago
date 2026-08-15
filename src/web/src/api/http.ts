function uuidv4(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

// CSRF token storage (ADR-0018-superseding BFF: double-submit pattern, token issued by Gateway on /auth/login).
let csrfToken: string | null = null;

export function setCsrfToken(token: string): void {
  csrfToken = token;
}

export function getCsrfToken(): string | null {
  return csrfToken;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  supportReference?: string;
  errors?: Record<string, string[]>;
  [key: string]: unknown;
}

export class HttpError extends Error {
  public statusCode: number;
  public problemDetails: ProblemDetails;

  constructor(statusCode: number, problemDetails: ProblemDetails, message: string) {
    super(message);
    this.statusCode = statusCode;
    this.problemDetails = problemDetails;
    this.name = 'HttpError';
  }
}

export class AuthenticationError extends HttpError {
  constructor(problemDetails: ProblemDetails) {
    super(401, problemDetails, 'Not authenticated (401)');
    this.name = 'AuthenticationError';
  }
}

export class AuthorizationError extends HttpError {
  constructor(problemDetails: ProblemDetails) {
    super(403, problemDetails, 'Not authorized (403)');
    this.name = 'AuthorizationError';
  }
}

export class ValidationError extends HttpError {
  public fieldErrors: Record<string, string[]>;

  constructor(problemDetails: ProblemDetails, fieldErrors: Record<string, string[]>) {
    super(400, problemDetails, 'Validation failed (400)');
    this.fieldErrors = fieldErrors;
    this.name = 'ValidationError';
  }
}

export class NotFoundError extends HttpError {
  constructor(problemDetails: ProblemDetails) {
    super(404, problemDetails, 'Resource not found (404)');
    this.name = 'NotFoundError';
  }
}

export class ConflictError extends HttpError {
  constructor(problemDetails: ProblemDetails) {
    super(409, problemDetails, 'Conflict - concurrency or duplicate (409)');
    this.name = 'ConflictError';
  }
}

export interface HttpClientConfig {
  baseUrl: string;
  headers?: Record<string, string>;
  credentials?: RequestCredentials;
  timeout?: number;
}

export interface RequestOptions {
  headers?: Record<string, string>;
  signal?: AbortSignal;
  timeout?: number;
}

export class HttpClient {
  private baseUrl: string;
  private defaultHeaders: Record<string, string>;
  private credentials: RequestCredentials;
  private defaultTimeout: number;

  constructor(config: HttpClientConfig) {
    this.baseUrl = config.baseUrl;
    this.defaultHeaders = {
      'Content-Type': 'application/json',
      ...config.headers,
    };
    this.credentials = config.credentials || 'same-origin';
    this.defaultTimeout = config.timeout || 30000;

    if (!this.isHttpsOrLocal()) {
      console.warn(
        'HttpClient: baseUrl should use HTTPS in production (SEC-021)',
      );
    }
  }

  private isHttpsOrLocal(): boolean {
    return (
      this.baseUrl.startsWith('https://') ||
      this.baseUrl.startsWith('http://localhost') ||
      this.baseUrl.startsWith('http://127.0.0.1')
    );
  }

  private normalizeUrl(path: string): string {
    const base = this.baseUrl.endsWith('/') ? this.baseUrl : `${this.baseUrl}/`;
    const cleanPath = path.startsWith('/') ? path.slice(1) : path;
    return `${base}${cleanPath}`;
  }

  private getCorrelationId(): string {
    return uuidv4();
  }

  private buildHeaders(
    options?: RequestOptions,
    correlationId?: string,
    method?: string,
  ): Record<string, string> {
    const headers: Record<string, string> = {
      ...this.defaultHeaders,
    };

    if (correlationId) {
      headers['X-Correlation-ID'] = correlationId;
      headers['traceparent'] = `00-${correlationId}-0000000000000000-01`;
    }

    // Attach CSRF token for mutating requests (POST, PUT, PATCH, DELETE) — ADR-0018-superseding.
    if (method && ['POST', 'PUT', 'PATCH', 'DELETE'].includes(method) && csrfToken) {
      headers['X-CSRF-TOKEN'] = csrfToken;
    }

    if (options?.headers) {
      Object.assign(headers, options.headers);
    }

    return headers;
  }

  private createAbortController(
    signal?: AbortSignal,
    timeout?: number,
  ): AbortController {
    const controller = new AbortController();
    const effectiveTimeout = timeout ?? this.defaultTimeout;

    if (effectiveTimeout > 0) {
      const timeoutId = setTimeout(
        () => controller.abort(),
        effectiveTimeout,
      );
      if (signal) {
        signal.addEventListener('abort', () => clearTimeout(timeoutId));
      }
    }

    if (signal) {
      signal.addEventListener('abort', () => controller.abort());
    }

    return controller;
  }

  private async parseErrorResponse(
    response: Response,
  ): Promise<ProblemDetails> {
    try {
      const text = await response.text();
      if (!text) {
        return {
          status: response.status,
          title: response.statusText || 'Unknown Error',
        };
      }
      return JSON.parse(text);
    } catch {
      return {
        status: response.status,
        title: response.statusText || 'Unknown Error',
      };
    }
  }

  private async handleErrorResponse(response: Response): Promise<never> {
    const problemDetails = await this.parseErrorResponse(response);
    const correlationId = response.headers.get('X-Correlation-ID');
    const traceId = response.headers.get('X-Trace-ID');

    const error: ProblemDetails = {
      ...problemDetails,
      traceId: traceId || problemDetails.traceId,
      supportReference: correlationId || problemDetails.supportReference,
    };

    switch (response.status) {
      case 401:
        throw new AuthenticationError(error);
      case 403:
        throw new AuthorizationError(error);
      case 400:
        throw new ValidationError(error, error.errors || {});
      case 404:
        throw new NotFoundError(error);
      case 409:
        throw new ConflictError(error);
      default:
        throw new HttpError(response.status, error, `HTTP ${response.status}`);
    }
  }

  async get<T = unknown>(
    path: string,
    options?: RequestOptions,
  ): Promise<T> {
    return this.request<T>('GET', path, undefined, options);
  }

  async post<T = unknown, B = unknown>(
    path: string,
    body?: B,
    options?: RequestOptions,
  ): Promise<T> {
    return this.request<T>('POST', path, body, options);
  }

  async put<T = unknown, B = unknown>(
    path: string,
    body?: B,
    options?: RequestOptions,
  ): Promise<T> {
    return this.request<T>('PUT', path, body, options);
  }

  async patch<T = unknown, B = unknown>(
    path: string,
    body?: B,
    options?: RequestOptions,
  ): Promise<T> {
    return this.request<T>('PATCH', path, body, options);
  }

  async delete<T = unknown>(
    path: string,
    options?: RequestOptions,
  ): Promise<T> {
    return this.request<T>('DELETE', path, undefined, options);
  }

  private async request<T>(
    method: string,
    path: string,
    body?: unknown,
    options?: RequestOptions,
  ): Promise<T> {
    const correlationId = this.getCorrelationId();
    const url = this.normalizeUrl(path);
    const headers = this.buildHeaders(options, correlationId, method);
    const controller = this.createAbortController(
      options?.signal,
      options?.timeout,
    );

    const fetchOptions: RequestInit = {
      method,
      headers,
      credentials: this.credentials,
      signal: controller.signal,
    };

    if (body !== undefined) {
      fetchOptions.body = JSON.stringify(body);
    }

    try {
      const response = await fetch(url, fetchOptions);

      // Capture CSRF token from /auth/login response header (ADR-0018-superseding BFF).
      if (method === 'POST' && path.endsWith('/auth/login') && response.ok) {
        const token = response.headers.get('X-CSRF-TOKEN');
        if (token) {
          setCsrfToken(token);
        }
      }

      if (!response.ok) {
        await this.handleErrorResponse(response);
      }

      if (response.status === 204) {
        return undefined as unknown as T;
      }

      const contentType = response.headers.get('content-type');
      if (!contentType?.includes('application/json')) {
        return undefined as unknown as T;
      }

      return await response.json();
    } catch (error) {
      if (error instanceof HttpError) {
        throw error;
      }

      if (error instanceof TypeError) {
        const message = error.message.toLowerCase();
        if (message.includes('fetch') || message.includes('network')) {
          throw new HttpError(0, {}, `Network error: ${error.message}`);
        }
      }

      if (error instanceof DOMException && error.name === 'AbortError') {
        throw new HttpError(0, {}, 'Request cancelled');
      }

      throw error;
    }
  }
}

export function createGatewayClient(baseUrl?: string): HttpClient {
  const url = baseUrl || import.meta.env.VITE_API_BASE_URL || '';

  if (!url) {
    console.warn(
      'Gateway base URL not configured. Set VITE_API_BASE_URL environment variable or pass baseUrl to createGatewayClient().',
    );
  }

  return new HttpClient({
    baseUrl: url,
    credentials: 'include',
  });
}
