import { describe, it, expect } from 'vitest';
import { navigationItems, administrationItems, hasRequiredRole } from './navigation';

describe('Navigation Configuration', () => {
  describe('navigationItems', () => {
    it('should have Dashboard', () => {
      const dashboard = navigationItems.find((item) => item.name === 'Dashboard');
      expect(dashboard).toBeDefined();
      expect(dashboard?.path).toBe('/dashboard');
    });

    it('should have Clients', () => {
      const clients = navigationItems.find((item) => item.name === 'Clients');
      expect(clients).toBeDefined();
      expect(clients?.path).toBe('/clients');
    });

    it('should have Projects', () => {
      const projects = navigationItems.find((item) => item.name === 'Projects');
      expect(projects).toBeDefined();
      expect(projects?.path).toBe('/projects');
    });

    it('should have Tasks', () => {
      const tasks = navigationItems.find((item) => item.name === 'Tasks');
      expect(tasks).toBeDefined();
      expect(tasks?.path).toBe('/tasks');
    });

    it('should have Search', () => {
      const search = navigationItems.find((item) => item.name === 'Search');
      expect(search).toBeDefined();
      expect(search?.path).toBe('/search');
    });

    it('should not require roles for main navigation items', () => {
      navigationItems.forEach((item) => {
        expect(item.requiredRoles).toBeUndefined();
      });
    });
  });

  describe('administrationItems', () => {
    it('should have Administration', () => {
      const admin = administrationItems.find((item) => item.name === 'Administration');
      expect(admin).toBeDefined();
      expect(admin?.path).toBe('/admin');
    });

    it('should require Admin role for Administration item', () => {
      const admin = administrationItems.find((item) => item.name === 'Administration');
      expect(admin?.requiredRoles).toEqual(['Admin']);
    });
  });

  describe('hasRequiredRole', () => {
    it('should return true when no roles are required', () => {
      expect(hasRequiredRole(['User'], undefined)).toBe(true);
      expect(hasRequiredRole(['User'], [])).toBe(true);
      expect(hasRequiredRole([], undefined)).toBe(true);
    });

    it('should return true when user has required role', () => {
      expect(hasRequiredRole(['Admin'], ['Admin'])).toBe(true);
      expect(hasRequiredRole(['Admin', 'User'], ['Admin'])).toBe(true);
    });

    it('should return false when user lacks required role', () => {
      expect(hasRequiredRole(['User'], ['Admin'])).toBe(false);
      expect(hasRequiredRole(['User'], ['Admin', 'Manager'])).toBe(false);
    });

    it('should return false when user has no roles', () => {
      expect(hasRequiredRole(undefined, ['Admin'])).toBe(false);
      expect(hasRequiredRole([], ['Admin'])).toBe(false);
    });

    it('should return true when user has any required role', () => {
      expect(hasRequiredRole(['Manager'], ['Admin', 'Manager'])).toBe(true);
      expect(hasRequiredRole(['Contributor'], ['Admin', 'Manager', 'Contributor'])).toBe(true);
    });
  });
});
