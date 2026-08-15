# HTTP API Client

Centralized typed HTTP client for communicating with the YARP gateway. Satisfies requirements API-001..007, ERROR-001..005, TRACE-001..007, SEC-020..025.

## Features

- **YARP Gateway Only**: All requests target the YARP base URL only (SEC-020). No direct service URLs.
- **Consistent REST Conventions**: REST-oriented routes and conventional HTTP verbs (API-001..003).
- **Automatic Correlation**: Every request includes unique `X-Correlation-ID` and `traceparent` headers (TRACE-001..007).
- **ProblemDetails Mapping**: Standardized error response handling with safe error details (ERROR-001..005).
- **Error Classification**: Distinguishes authentication (401), authorization (403), validation (400), not-found (404), conflict (409), and internal errors.
- **Cancellation Support**: Supports `AbortSignal` for request cancellation.
- **Type Safety**: Fully typed request/response contracts with TypeScript.
- **Security**: No secrets logged, HTTPS enforcement, same-origin credentials (SEC-021..025).

## Usage

### Basic Setup

Configure the gateway client at application startup:

```typescript
import { initializeGatewayClient } from '@/api';

// In your App.tsx or main.tsx
const baseUrl = import.meta.env.VITE_API_BASE_URL || 'https://api.example.com';
initializeGatewayClient(baseUrl);
```

### Making Requests

```typescript
import { getGatewayClient } from '@/api';

const client = getGatewayClient();

// GET
const client = await client.get<ClientDto>('/api/clients/123');

// POST with body
const created = await client.post<ClientDto>('/api/clients', {
  name: 'Acme Corp',
  email: 'contact@acme.com',
});

// PUT
await client.put('/api/clients/123', { name: 'Updated Name' });

// PATCH
await client.patch('/api/clients/123', { status: 'archived' });

// DELETE
await client.delete('/api/clients/123');
```

### Request Options

```typescript
const controller = new AbortController();

// With cancellation
const promise = client.get('/api/clients', { 
  signal: controller.signal 
});

// With timeout
const data = await client.get('/api/clients', { 
  timeout: 5000 
});

// With custom headers
const data = await client.get('/api/clients', { 
  headers: { 'X-Custom-Header': 'value' } 
});
```

### Error Handling

```typescript
import { 
  AuthenticationError, 
  AuthorizationError, 
  ValidationError,
  NotFoundError,
  HttpError 
} from '@/api';

try {
  const result = await client.post('/api/clients', data);
} catch (error) {
  if (error instanceof AuthenticationError) {
    // User is not authenticated (401)
    // Redirect to login
  } else if (error instanceof AuthorizationError) {
    // User lacks permission (403)
    // Show forbidden message
  } else if (error instanceof ValidationError) {
    // Server-side validation failed (400)
    const fieldErrors = error.fieldErrors; // { name: ['Name is required'] }
    // Display field errors
  } else if (error instanceof NotFoundError) {
    // Resource not found (404)
  } else if (error instanceof HttpError) {
    // Other HTTP error
    const traceId = error.problemDetails.traceId;
    const supportRef = error.problemDetails.supportReference;
    // Show error with trace/support reference
  }
}
```

## ProblemDetails Response Format

All error responses follow the RFC 9457 ProblemDetails format:

```json
{
  "type": "https://api.example.com/errors/validation",
  "title": "Validation Failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/clients",
  "traceId": "0af3e8c0-...",
  "supportReference": "uuid-...",
  "errors": {
    "name": ["Name is required"],
    "email": ["Invalid email format"]
  }
}
```

## Correlation Headers

Every request automatically includes:

- `X-Correlation-ID`: Unique UUID per request for tracing
- `traceparent`: W3C Trace Context header for distributed tracing

These are used to correlate logs and traces across services.

## Configuration

### Environment Variables

Set `VITE_API_BASE_URL` in your `.env` file:

```
VITE_API_BASE_URL=https://api.example.com
```

### Default Timeout

Default request timeout is 30 seconds. Override per-request or at client creation:

```typescript
const client = new HttpClient({
  baseUrl: 'https://api.example.com',
  timeout: 60000, // 60 seconds
});
```

## Security Considerations

- All requests use `same-origin` credentials by default (no cross-origin cookies)
- Secrets, tokens, and connection strings are never logged
- HTTPS is enforced in production; localhost HTTP is allowed for development
- PII and sensitive data are not automatically logged in telemetry
