import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ThemeProvider } from '@/context/ThemeContext';
import { AuthProvider, ProtectedRoute, LoginPage } from '@/auth';
import AuthenticatedShell from './app/AuthenticatedShell';
import { ClientsListPage } from './features/clients/ClientsListPage';

function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <BrowserRouter>
          <Routes>
            {/* Public routes */}
            <Route path="/login" element={<LoginPage />} />

            {/* Protected routes */}
            <Route
              element={
                <ProtectedRoute>
                  <AuthenticatedShell />
                </ProtectedRoute>
              }
            >
              <Route path="/dashboard" element={<PagePlaceholder title="Dashboard" />} />
              <Route path="/clients" element={<ClientsListPage />} />
              <Route path="/projects" element={<PagePlaceholder title="Projects" />} />
              <Route path="/tasks" element={<PagePlaceholder title="Tasks" />} />
              <Route path="/search" element={<PagePlaceholder title="Search" />} />
              <Route path="/admin" element={<ProtectedRoute requiredRoles={['Admin']}><PagePlaceholder title="Administration" /></ProtectedRoute>} />
              <Route index element={<Navigate to="/dashboard" replace />} />
            </Route>

            {/* Catch-all redirect */}
            <Route path="*" element={<Navigate to="/dashboard" replace />} />
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

interface PagePlaceholderProps {
  title: string;
}

function PagePlaceholder({ title }: PagePlaceholderProps) {
  return (
    <div>
      <h1>{title}</h1>
      <p>Feature page for {title.toLowerCase()} coming soon.</p>
    </div>
  );
}

export default App;
