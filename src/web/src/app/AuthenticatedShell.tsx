import type { FC } from 'react';
import { SidebarProvider } from '@/context/SidebarContext';
import AppLayout from '@/layout/AppLayout';

/**
 * Authenticated application shell that wraps all authenticated routes.
 * Provides Sidebar context required by AppLayout.
 * The AppLayout component renders the header, sidebar, and content Outlet.
 */
const AuthenticatedShell: FC = () => {
  return (
    <SidebarProvider>
      <AppLayout />
    </SidebarProvider>
  );
};

export default AuthenticatedShell;
