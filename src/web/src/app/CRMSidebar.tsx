import type { FC } from 'react';
import AppSidebar from '@/layout/AppSidebar';
import { useAuth } from '@/auth';
import { navigationItems, administrationItems } from './navigation';

const CRMSidebar: FC = () => {
  const { currentUser } = useAuth();

  return (
    <AppSidebar
      navItems={navigationItems}
      administrationItems={administrationItems}
      currentUserRoles={currentUser?.roles}
    />
  );
};

export default CRMSidebar;
