import type { FC } from 'react';
import { GridIcon, UserCircleIcon, BoxCubeIcon, ListIcon, PageIcon, PlugInIcon } from '@/icons';

export interface NavItem {
  name: string;
  icon: FC<React.SVGProps<SVGSVGElement>>;
  path: string;
  requiredRoles?: string[];
}

export const navigationItems: NavItem[] = [
  {
    name: 'Dashboard',
    icon: GridIcon,
    path: '/dashboard',
  },
  {
    name: 'Clients',
    icon: UserCircleIcon,
    path: '/clients',
  },
  {
    name: 'Projects',
    icon: BoxCubeIcon,
    path: '/projects',
  },
  {
    name: 'Tasks',
    icon: ListIcon,
    path: '/tasks',
  },
  {
    name: 'Search',
    icon: PageIcon,
    path: '/search',
  },
];

export const administrationItems: NavItem[] = [
  {
    name: 'Administration',
    icon: PlugInIcon,
    path: '/admin',
    requiredRoles: ['Admin'],
  },
];

export function hasRequiredRole(
  userRoles: string[] | undefined,
  requiredRoles?: string[],
): boolean {
  if (!requiredRoles || requiredRoles.length === 0) {
    return true;
  }
  if (!userRoles || userRoles.length === 0) {
    return false;
  }
  return requiredRoles.some((role) => userRoles.includes(role));
}
