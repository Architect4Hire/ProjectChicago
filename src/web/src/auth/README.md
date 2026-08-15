# Authentication Module

Client-side authentication state management using ASP.NET Core Identity as the server-side identity system. Satisfies requirements SEC-001..016 and UX-003..005.

## Features

- **ASP.NET Core Identity Integration**: Uses the server-side Identity system without implementing credential handling in React
- **Automatic Session Loading**: Checks current user on app initialization
- **Protected Routes**: Route guard component redirects unauthenticated users to login
- **Role-Based Access Control**: Optional role requirements on protected routes
- **Error Distinction**: Handles 401 (authentication) vs 403 (authorization) separately
- **Secure Credential Handling**: Never persists passwords or long-lived secrets in browser storage
- **PCDS Design Patterns**: Uses design system components for loading and error states

## Usage

### Setup

Wrap your app with `AuthProvider`:

```tsx
import { AuthProvider } from '@/auth';

export function App() {
  return (
    <AuthProvider>
      {/* Routes */}
    </AuthProvider>
  );
}
```

### Using the Auth Hook

```tsx
import { useAuth } from '@/auth';

function UserGreeting() {
  const { currentUser, isAuthenticated, logout } = useAuth();

  if (!isAuthenticated) {
    return <div>Please log in</div>;
  }

  return (
    <div>
      <p>Welcome, {currentUser?.userName}</p>
      <button onClick={() => logout()}>Sign Out</button>
    </div>
  );
}
```

### Protecting Routes

```tsx
import { ProtectedRoute } from '@/auth';

<Route
  path="/dashboard"
  element={
    <ProtectedRoute>
      <DashboardPage />
    </ProtectedRoute>
  }
/>
```

### Role-Based Access Control

```tsx
<Route
  path="/admin"
  element={
    <ProtectedRoute requiredRoles={['Admin']}>
      <AdminPanel />
    </ProtectedRoute>
  }
/>
```

## Architecture

### AuthProvider

Manages global authentication state including:
- Current user information
- Authentication status
- Loading state during API calls
- Error messages

**API Contract** (Expected endpoints):

```
GET /auth/me
  - Returns: { id, email, userName, roles }
  - Throws: AuthenticationError (401) if not authenticated

POST /auth/login
  - Body: { email, secret }
  - Returns: { user: { id, email, userName, roles } }
  - Throws: AuthenticationError (401) for invalid credentials

POST /auth/logout
  - Body: {}
  - Returns: {}
```

### ProtectedRoute

Renders one of three states:

1. **Loading**: Shows spinner while checking authentication
2. **Unauthenticated**: Redirects to `/login`
3. **Authenticated**: Renders protected content, or shows access denied if role requirements not met

### LoginPage

Pre-built login form using PCDS design system:
- Email and secret input fields
- Client-side validation
- Error display with server-side messages
- Loading state while submitting
- Keyboard accessible

## Error Handling

| Scenario | Error Type | User Message |
|----------|-----------|--------------|
| Not logged in | 401 Unauthenticated | Redirect to login |
| Invalid credentials | 401 Unauthorized | "Invalid email or password" |
| Server error | 5xx HttpError | "Server error" / custom detail |
| Network error | TypeError | Generic error message |
| Missing role | 403 Forbidden | "Access Denied" page |

## Security Considerations

- **No Browser Storage**: Credentials are never stored in localStorage/sessionStorage
- **HTTPS Only**: HTTP client enforces HTTPS in production (localhost allowed for dev)
- **No Logging Secrets**: Password/token fields are never logged
- **Same-Origin Cookies**: Uses same-origin credentials to prevent CSRF
- **Server Authorization**: Client-side role checks are UX improvements only; server enforces actual authorization

## Testing

Run tests with:

```bash
npm test
```

Tests cover:
- Initial user load on app startup
- Login with valid/invalid credentials
- Logout with success/failure
- Unauthenticated redirects
- Role-based access control
- Loading and error states
- 401/403 error distinction

## Future Enhancements

- [ ] Token refresh logic (when cookie/token strategy is decided)
- [ ] Multi-tenant support (if applicable)
- [ ] MFA/passkey support
- [ ] External provider integration (OAuth, SAML, etc.)
- [ ] Account recovery flows
