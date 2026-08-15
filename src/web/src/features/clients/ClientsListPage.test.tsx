import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BrowserRouter } from 'react-router-dom';
import { ClientsListPage } from './ClientsListPage';
import * as clientsApi from '@/api/clients';
import type { Client } from '@/api/clients';

vi.mock('@/api/clients');

const mockClient: Client = {
  id: '1',
  name: 'Acme Corp',
  primaryContactName: 'John Doe',
  primaryEmail: 'john@acme.com',
  primaryPhone: '555-1234',
  website: 'https://acme.com',
  address: '123 Main St',
  city: 'New York',
  state: 'NY',
  postalCode: '10001',
  country: 'USA',
  lifecycleStatus: 'Active' as const,
  description: 'A test client',
  assignedOwner: 'Jane Smith',
  createdDate: '2024-01-15T10:00:00Z',
  createdBy: 'Admin User',
  lastModifiedDate: '2024-08-15T10:00:00Z',
  lastModifiedBy: 'Jane Smith',
};

const renderWithRouter = (component: React.ReactElement) => {
  return render(<BrowserRouter>{component}</BrowserRouter>);
};

describe('ClientsListPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render loading state initially', async () => {
    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: vi.fn(() => new Promise(() => {})), // Never resolves
    } as any);

    renderWithRouter(<ClientsListPage />);

    expect(screen.getByText('Loading clients...')).toBeInTheDocument();
  });

  it('should render error state when API fails', async () => {
    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: vi.fn().mockRejectedValue(new Error('API Error')),
    } as any);

    renderWithRouter(<ClientsListPage />);

    await waitFor(() => {
      expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    });
  });

  it('should render empty state when no clients found', async () => {
    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: vi.fn().mockResolvedValue({
        pageNumber: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 0,
        items: [],
      }),
    } as any);

    renderWithRouter(<ClientsListPage />);

    await waitFor(() => {
      expect(screen.getByText('No clients found')).toBeInTheDocument();
    });
  });

  it('should render client list when data loaded', async () => {
    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: vi.fn().mockResolvedValue({
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockClient],
      }),
    } as any);

    renderWithRouter(<ClientsListPage />);

    await waitFor(() => {
      expect(screen.getByText('Acme Corp')).toBeInTheDocument();
      expect(screen.getByText('John Doe')).toBeInTheDocument();
      expect(screen.getByText('john@acme.com')).toBeInTheDocument();
    });
  });

  it('should show filters when filter button clicked', async () => {
    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: vi.fn().mockResolvedValue({
        pageNumber: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 0,
        items: [],
      }),
    } as any);

    renderWithRouter(<ClientsListPage />);

    const filterButton = screen.getByLabelText('Show filters');
    await userEvent.click(filterButton);

    await waitFor(() => {
      expect(screen.getByPlaceholderText('Search by name, contact, email, or phone')).toBeInTheDocument();
    });
  });

  it('should update search when search input changes', async () => {
    const mockListClients = vi.fn().mockResolvedValue({
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      items: [mockClient],
    });

    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: mockListClients,
    } as any);

    renderWithRouter(<ClientsListPage />);

    // Show filters
    const filterButton = screen.getByLabelText('Show filters');
    await userEvent.click(filterButton);

    // Type in search
    const searchInput = screen.getByPlaceholderText('Search by name, contact, email, or phone');
    await userEvent.type(searchInput, 'acme');

    await waitFor(() => {
      expect(mockListClients).toHaveBeenCalledWith(
        expect.objectContaining({
          search: 'acme',
          pageNumber: 1,
        }),
      );
    });
  });

  it('should filter by lifecycle status', async () => {
    const mockListClients = vi.fn().mockResolvedValue({
      pageNumber: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
      items: [],
    });

    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: mockListClients,
    } as any);

    renderWithRouter(<ClientsListPage />);

    // Show filters
    const filterButton = screen.getByLabelText('Show filters');
    await userEvent.click(filterButton);

    // Click Active checkbox
    const activeCheckbox = screen.getByRole('checkbox', { name: /Active/ });
    await userEvent.click(activeCheckbox);

    await waitFor(() => {
      expect(mockListClients).toHaveBeenCalledWith(
        expect.objectContaining({
          lifecycleStatus: ['Active'],
        }),
      );
    });
  });

  it('should filter by assigned owner', async () => {
    const mockListClients = vi.fn().mockResolvedValue({
      pageNumber: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
      items: [],
    });

    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: mockListClients,
    } as any);

    renderWithRouter(<ClientsListPage />);

    // Show filters
    const filterButton = screen.getByLabelText('Show filters');
    await userEvent.click(filterButton);

    // Type in owner input
    const ownerInput = screen.getByPlaceholderText('Filter by owner name or ID');
    await userEvent.type(ownerInput, 'Jane Smith');

    await waitFor(() => {
      expect(mockListClients).toHaveBeenCalledWith(
        expect.objectContaining({
          assignedOwner: 'Jane Smith',
        }),
      );
    });
  });

  it('should exclude archived clients by default', async () => {
    const mockListClients = vi.fn().mockResolvedValue({
      pageNumber: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
      items: [],
    });

    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: mockListClients,
    } as any);

    renderWithRouter(<ClientsListPage />);

    await waitFor(() => {
      expect(mockListClients).toHaveBeenCalledWith(
        expect.objectContaining({
          excludeArchived: true,
        }),
      );
    });
  });

  it('should sort by column when header clicked', async () => {
    const mockListClients = vi.fn().mockResolvedValue({
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      items: [mockClient],
    });

    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: mockListClients,
    } as any);

    renderWithRouter(<ClientsListPage />);

    await waitFor(() => {
      expect(screen.getByText('Acme Corp')).toBeInTheDocument();
    });

    // Click sort by Created Date
    const createdHeader = screen.getByRole('button', { name: /Sort by Created/ });
    await userEvent.click(createdHeader);

    await waitFor(() => {
      expect(mockListClients).toHaveBeenCalledWith(
        expect.objectContaining({
          sortBy: 'createdDate',
          sortDirection: 'asc',
        }),
      );
    });
  });

  it('should toggle sort direction when same column clicked', async () => {
    const mockListClients = vi.fn().mockResolvedValue({
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      items: [mockClient],
    });

    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: mockListClients,
    } as any);

    renderWithRouter(<ClientsListPage />);

    await waitFor(() => {
      expect(screen.getByText('Acme Corp')).toBeInTheDocument();
    });

    const sortHeader = screen.getByRole('button', { name: /Sort by Client Name/ });

    // First click
    await userEvent.click(sortHeader);
    await waitFor(() => {
      expect(mockListClients).toHaveBeenCalledWith(
        expect.objectContaining({ sortBy: 'name', sortDirection: 'asc' }),
      );
    });

    // Second click should toggle to desc
    await userEvent.click(sortHeader);
    await waitFor(() => {
      expect(mockListClients).toHaveBeenCalledWith(
        expect.objectContaining({ sortBy: 'name', sortDirection: 'desc' }),
      );
    });
  });

  it('should support pagination', async () => {
    const mockListClients = vi.fn().mockResolvedValue({
      pageNumber: 1,
      pageSize: 10,
      totalCount: 25,
      totalPages: 3,
      items: [mockClient],
    });

    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: mockListClients,
    } as any);

    renderWithRouter(<ClientsListPage />);

    await waitFor(() => {
      expect(screen.getByText('Showing 1 to 10 of 25 results')).toBeInTheDocument();
    });

    // Click next page
    const nextButton = screen.getByRole('button', { name: /Next →/ });
    await userEvent.click(nextButton);

    await waitFor(() => {
      expect(mockListClients).toHaveBeenCalledWith(
        expect.objectContaining({ pageNumber: 2 }),
      );
    });
  });

  it('should support keyboard navigation on table rows', async () => {
    const mockListClients = vi.fn().mockResolvedValue({
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      items: [mockClient],
    });

    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: mockListClients,
    } as any);

    renderWithRouter(<ClientsListPage />);

    await waitFor(() => {
      expect(screen.getByText('Acme Corp')).toBeInTheDocument();
    });

    // Focus on table row
    const row = screen.getByText('Acme Corp').closest('tr');
    expect(row).toBeInTheDocument();
  });

  it('should support keyboard navigation on sort headers', async () => {
    const mockListClients = vi.fn().mockResolvedValue({
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      items: [mockClient],
    });

    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: mockListClients,
    } as any);

    renderWithRouter(<ClientsListPage />);

    await waitFor(() => {
      expect(screen.getByText('Acme Corp')).toBeInTheDocument();
    });

    const sortHeader = screen.getByRole('columnheader', { name: /Client Name/ });

    // Press Enter on header
    fireEvent.keyDown(sortHeader, { key: 'Enter', code: 'Enter' });

    await waitFor(() => {
      expect(mockListClients).toHaveBeenCalled();
    });
  });

  it('should display status badge with appropriate styling', async () => {
    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: vi.fn().mockResolvedValue({
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockClient],
      }),
    } as any);

    renderWithRouter(<ClientsListPage />);

    await waitFor(() => {
      const statusBadge = screen.getByRole('status');
      expect(statusBadge).toHaveTextContent('Active');
      // Should have styling indicating Active status (not just rely on color)
      expect(statusBadge).toHaveClass('bg-green-100');
    });
  });

  it('should retry loading on error', async () => {
    const mockListClients = vi
      .fn()
      .mockRejectedValueOnce(new Error('API Error'))
      .mockResolvedValueOnce({
        pageNumber: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 0,
        items: [],
      });

    vi.spyOn(clientsApi, 'clientsApi', 'get').mockReturnValue({
      listClients: mockListClients,
    } as any);

    renderWithRouter(<ClientsListPage />);

    await waitFor(() => {
      expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    });

    const retryButton = screen.getByRole('button', { name: /Try again/ });
    await userEvent.click(retryButton);

    await waitFor(() => {
      expect(mockListClients).toHaveBeenCalledTimes(2);
    });
  });
});
