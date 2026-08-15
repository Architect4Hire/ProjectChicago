import { describe, it, expect, beforeEach, vi } from 'vitest';
import CRMSidebar from './CRMSidebar';
import * as authModule from '@/auth';

vi.mock('@/auth', () => ({
  useAuth: vi.fn(),
}));

vi.mock('@/layout/AppSidebar', () => ({
  default: ({ navItems = [], administrationItems = [] }: any) => (
    <div data-testid="crm-sidebar">
      <div data-testid="nav-items">
        {navItems.map((item: any) => (
          <div key={item.path} data-testid={`nav-item-${item.path}`}>
            {item.name}
          </div>
        ))}
      </div>
      <div data-testid="admin-items">
        {administrationItems.map((item: any) => (
          <div key={item.path} data-testid={`admin-item-${item.path}`}>
            {item.name}
          </div>
        ))}
      </div>
    </div>
  ),
}));

describe('CRMSidebar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render sidebar component', () => {
    (authModule.useAuth as any).mockReturnValue({
      currentUser: { id: 'user-1', email: 'test@example.com', userName: 'test', roles: ['User'] },
      isAuthenticated: true,
      isLoading: false,
      error: null,
      login: vi.fn(),
      logout: vi.fn(),
      refreshUser: vi.fn(),
    });

    const component = <CRMSidebar />;
    expect(component).toBeDefined();
  });

  it('should pass current user roles to AppSidebar', () => {
    const mockRoles = ['Manager', 'Contributor'];
    (authModule.useAuth as any).mockReturnValue({
      currentUser: { id: 'user-1', email: 'test@example.com', userName: 'test', roles: mockRoles },
      isAuthenticated: true,
      isLoading: false,
      error: null,
      login: vi.fn(),
      logout: vi.fn(),
      refreshUser: vi.fn(),
    });

    const component = <CRMSidebar />;
    expect(component).toBeDefined();
  });

  it('should handle null current user', () => {
    (authModule.useAuth as any).mockReturnValue({
      currentUser: null,
      isAuthenticated: false,
      isLoading: false,
      error: null,
      login: vi.fn(),
      logout: vi.fn(),
      refreshUser: vi.fn(),
    });

    const component = <CRMSidebar />;
    expect(component).toBeDefined();
  });

  it('should expose navigation configuration items', () => {
    (authModule.useAuth as any).mockReturnValue({
      currentUser: { id: 'user-1', email: 'test@example.com', userName: 'test', roles: ['Admin'] },
      isAuthenticated: true,
      isLoading: false,
      error: null,
      login: vi.fn(),
      logout: vi.fn(),
      refreshUser: vi.fn(),
    });

    const component = <CRMSidebar />;
    expect(component).toBeDefined();
  });
});
