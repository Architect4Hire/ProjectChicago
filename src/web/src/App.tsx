import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ThemeProvider } from '@/context/ThemeContext';
import AuthenticatedShell from './app/AuthenticatedShell';

function App() {
  return (
    <ThemeProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<AuthenticatedShell />}>
            <Route path="/dashboard" element={<DashboardPlaceholder />} />
            <Route index element={<Navigate to="/dashboard" replace />} />
          </Route>
          <Route path="*" element={<Navigate to="/dashboard" replace />} />
        </Routes>
      </BrowserRouter>
    </ThemeProvider>
  );
}

// Minimal placeholder to prove shell mounts and Outlet works
function DashboardPlaceholder() {
  return (
    <div>
      <h1>Project Chicago CRM</h1>
      <p>Application shell ready for feature pages.</p>
    </div>
  );
}

export default App;
