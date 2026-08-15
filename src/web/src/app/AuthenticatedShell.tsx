import type { FC } from 'react';
import AppLayout from '@/layout/AppLayout';
import CRMSidebar from './CRMSidebar';

/**
 * Authenticated application shell that wraps all authenticated routes.
 * Provides the CRM-configured sidebar with Project Chicago navigation.
 * The AppLayout component renders the header, sidebar, and content Outlet.
 */
const AuthenticatedShell: FC = () => {
  return <AppLayout sidebar={CRMSidebar} />;
};

export default AuthenticatedShell;
