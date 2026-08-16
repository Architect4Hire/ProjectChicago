import { describe, it, expect, beforeEach, vi } from 'vitest';
import { clientsApi } from './clients';
import * as gatewayModule from './gateway';

vi.mock('./gateway', () => ({
  getGatewayClient: vi.fn(),
}));

const mockClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
};

const mockClients = [
  {
    id: 'client-1',
    name: 'Acme Corp',
    primaryContactName: 'John Doe',
    primaryEmail: 'john@acme.com',
    primaryPhone: '555-0100',
    website: 'https://acme.com',
    address: '123 Main St',
    city: 'Springfield',
    state: 'IL',
    postalCode: '62701',
    country: 'USA',
    lifecycleStatus: 'Active' as const,
    description: 'Leading manufacturing company',
    assignedOwner: 'user-1',
    createdDate: '2024-01-01T00:00:00Z',
    createdBy: 'admin',
    lastModifiedDate: '2024-08-15T10:30:00Z',
    lastModifiedBy: 'user-1',
  },
  {
    id: 'client-2',
    name: 'TechStart Inc',
    primaryContactName: 'Jane Smith',
    primaryEmail: 'jane@techstart.com',
    primaryPhone: '555-0200',
    website: 'https://techstart.com',
    address: '456 Tech Ave',
    city: 'San Francisco',
    state: 'CA',
    postalCode: '94102',
    country: 'USA',
    lifecycleStatus: 'Lead' as const,
    description: 'Software startup',
    assignedOwner: 'user-2',
    createdDate: '2024-08-01T00:00:00Z',
    createdBy: 'user-2',
    lastModifiedDate: '2024-08-15T09:15:00Z',
    lastModifiedBy: 'user-2',
  },
];

describe('clientsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (gatewayModule.getGatewayClient as any).mockReturnValue(mockClient);
  });

  describe('listClients', () => {
    it('should fetch clients list without options', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 2,
        totalPages: 1,
        items: mockClients,
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      const result = await clientsApi.listClients();

      expect(mockClient.get).toHaveBeenCalledWith('/api/clients');
      expect(result).toEqual(mockResponse);
      expect(result.items).toHaveLength(2);
    });

    it('should fetch clients list with pagination options', async () => {
      const mockResponse = {
        pageNumber: 2,
        pageSize: 10,
        totalCount: 15,
        totalPages: 2,
        items: [mockClients[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      const result = await clientsApi.listClients({
        pageNumber: 2,
        pageSize: 10,
      });

      expect(mockClient.get).toHaveBeenCalledWith(
        expect.stringContaining('/api/clients?')
      );
      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('pageNumber=2');
      expect(callArg).toContain('pageSize=10');
      expect(result.pageNumber).toBe(2);
    });

    it('should fetch clients list with search parameter', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockClients[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      const result = await clientsApi.listClients({
        search: 'Acme',
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('search=Acme');
      expect(result.items).toHaveLength(1);
    });

    it('should fetch clients list with lifecycle status filter', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockClients[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await clientsApi.listClients({
        lifecycleStatus: 'Active',
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('lifecycleStatus=Active');
    });

    it('should fetch clients list with multiple lifecycle statuses', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 2,
        totalPages: 1,
        items: mockClients,
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await clientsApi.listClients({
        lifecycleStatus: ['Active', 'Lead'],
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('lifecycleStatus=Active');
      expect(callArg).toContain('lifecycleStatus=Lead');
    });

    it('should fetch clients list with assigned owner filter', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockClients[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await clientsApi.listClients({
        assignedOwner: 'user-1',
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('assignedOwner=user-1');
    });

    it('should fetch clients list with exclude archived flag', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockClients[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await clientsApi.listClients({
        excludeArchived: true,
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('excludeArchived=true');
    });

    it('should fetch clients list with sort options', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 2,
        totalPages: 1,
        items: mockClients,
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await clientsApi.listClients({
        sortBy: 'name',
        sortDirection: 'Ascending',
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('sortBy=name');
      expect(callArg).toContain('sortDirection=Ascending');
    });

    it('should fetch clients list with combined options', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 10,
        totalCount: 1,
        totalPages: 1,
        items: [mockClients[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await clientsApi.listClients({
        pageNumber: 1,
        pageSize: 10,
        search: 'Acme',
        lifecycleStatus: 'Active',
        assignedOwner: 'user-1',
        excludeArchived: true,
        sortBy: 'name',
        sortDirection: 'Descending',
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('pageNumber=1');
      expect(callArg).toContain('pageSize=10');
      expect(callArg).toContain('search=Acme');
      expect(callArg).toContain('lifecycleStatus=Active');
      expect(callArg).toContain('assignedOwner=user-1');
      expect(callArg).toContain('excludeArchived=true');
      expect(callArg).toContain('sortBy=name');
      expect(callArg).toContain('sortDirection=Descending');
    });
  });

  describe('getClient', () => {
    const mockClientDetail = {
      client: {
        id: 'client-1',
        name: 'Acme Corp',
        primaryContactName: 'John Doe',
        primaryEmail: 'john@acme.com',
        primaryPhone: '555-0100',
        website: 'https://acme.com',
        addressLine: '123 Main St',
        city: 'Springfield',
        stateOrProvince: 'IL',
        postalCode: '62701',
        country: 'USA',
        lifecycleStatus: 'Active' as const,
        description: 'Leading manufacturing company',
        ownerUserId: 'user-1',
        createdAtUtc: '2024-01-01T00:00:00Z',
        createdBy: 'admin',
        lastModifiedAtUtc: '2024-08-15T10:30:00Z',
        lastModifiedBy: 'user-1',
        concurrencyToken: 'AAAAAAAAB9E=',
      },
      activeProjects: [],
      historicalProjects: [],
      openTasks: [],
      recentlyCompletedTasks: [],
    };

    it('should fetch the consolidated Client detail by ID (CLIENT-030..032)', async () => {
      mockClient.get.mockResolvedValueOnce(mockClientDetail);

      const result = await clientsApi.getClient('client-1');

      expect(mockClient.get).toHaveBeenCalledWith('/api/clients/client-1', undefined);
      expect(result).toEqual(mockClientDetail);
      expect(result.client.id).toBe('client-1');
    });

    it('forwards request options (e.g. an abort signal) to the gateway client', async () => {
      const controller = new AbortController();
      mockClient.get.mockResolvedValueOnce(mockClientDetail);

      await clientsApi.getClient('client-1', { signal: controller.signal });

      expect(mockClient.get).toHaveBeenCalledWith('/api/clients/client-1', { signal: controller.signal });
    });
  });

  describe('createClient', () => {
    it('should create a new client with required fields', async () => {
      const createRequest = {
        name: 'New Client',
        ownerUserId: 'user-1',
        primaryContactName: 'Jane Doe',
        primaryEmail: 'jane@newclient.com',
        primaryPhone: '555-0300',
      };

      const createdClient = {
        ...mockClients[0],
        ...createRequest,
        id: 'client-new',
        createdDate: '2024-08-15T12:00:00Z',
        createdBy: 'current-user',
      };

      mockClient.post.mockResolvedValueOnce(createdClient);

      const result = await clientsApi.createClient(createRequest);

      expect(mockClient.post).toHaveBeenCalledWith('/api/clients', createRequest);
      expect(result.name).toBe('New Client');
      expect(result.id).toBe('client-new');
    });

    it('should create a client with all optional fields', async () => {
      const createRequest = {
        name: 'Full Client',
        ownerUserId: 'user-3',
        primaryContactName: 'John Smith',
        primaryEmail: 'john@fullclient.com',
        primaryPhone: '555-0400',
        website: 'https://fullclient.com',
        addressLine: '789 Business Blvd',
        city: 'New York',
        stateOrProvince: 'NY',
        postalCode: '10001',
        country: 'USA',
        description: 'Complete client information',
      };

      const createdClient = {
        ...mockClients[0],
        ...createRequest,
        id: 'client-full',
      };

      mockClient.post.mockResolvedValueOnce(createdClient);

      const result = await clientsApi.createClient(createRequest);

      expect(mockClient.post).toHaveBeenCalledWith('/api/clients', createRequest);
      expect(result.website).toBe('https://fullclient.com');
    });
  });

  describe('updateClient', () => {
    it('should update a client with partial fields', async () => {
      const updateRequest = {
        name: 'Updated Name',
        lifecycleStatus: 'Prospect' as const,
      };

      const updatedClient = {
        ...mockClients[0],
        ...updateRequest,
      };

      mockClient.put.mockResolvedValueOnce(updatedClient);

      const result = await clientsApi.updateClient('client-1', updateRequest);

      expect(mockClient.put).toHaveBeenCalledWith('/api/clients/client-1', updateRequest);
      expect(result.name).toBe('Updated Name');
      expect(result.lifecycleStatus).toBe('Prospect');
    });

    it('should update a client with multiple fields', async () => {
      const updateRequest = {
        primaryContactName: 'New Contact',
        primaryEmail: 'newcontact@acme.com',
        lifecycleStatus: 'Active' as const,
        description: 'Updated description',
      };

      const updatedClient = {
        ...mockClients[0],
        ...updateRequest,
      };

      mockClient.put.mockResolvedValueOnce(updatedClient);

      const result = await clientsApi.updateClient('client-1', updateRequest);

      expect(mockClient.put).toHaveBeenCalledWith('/api/clients/client-1', updateRequest);
      expect(result.primaryContactName).toBe('New Contact');
      expect(result.description).toBe('Updated description');
    });
  });

  describe('archiveClient', () => {
    it('should archive a client by ID with its expected concurrency token', async () => {
      const archivedClient = { ...mockClients[0], lifecycleStatus: 'Archived' as const };
      mockClient.post.mockResolvedValueOnce(archivedClient);

      const result = await clientsApi.archiveClient('client-1', { expectedConcurrencyToken: 'AAAAAAAAB9E=' });

      expect(mockClient.post).toHaveBeenCalledWith('/api/clients/client-1/archive', {
        expectedConcurrencyToken: 'AAAAAAAAB9E=',
      });
      expect(result.lifecycleStatus).toBe('Archived');
    });

    it('should archive different clients', async () => {
      mockClient.post.mockResolvedValueOnce(mockClients[1]);

      await clientsApi.archiveClient('client-2', { expectedConcurrencyToken: 'BBBBBBBBB9E=' });

      expect(mockClient.post).toHaveBeenCalledWith('/api/clients/client-2/archive', {
        expectedConcurrencyToken: 'BBBBBBBBB9E=',
      });
    });
  });

  describe('restoreClient', () => {
    it('should restore an archived client to a chosen lifecycle status with its expected concurrency token', async () => {
      const restoredClient = { ...mockClients[0], lifecycleStatus: 'Active' as const };
      mockClient.post.mockResolvedValueOnce(restoredClient);

      const result = await clientsApi.restoreClient('client-1', {
        restoredStatus: 'Active',
        expectedConcurrencyToken: 'AAAAAAAAB9E=',
      });

      expect(mockClient.post).toHaveBeenCalledWith('/api/clients/client-1/restore', {
        restoredStatus: 'Active',
        expectedConcurrencyToken: 'AAAAAAAAB9E=',
      });
      expect(result.lifecycleStatus).toBe('Active');
    });

    it('should restore different clients to different statuses', async () => {
      mockClient.post.mockResolvedValueOnce({ ...mockClients[1], lifecycleStatus: 'Lead' as const });

      await clientsApi.restoreClient('client-2', {
        restoredStatus: 'Lead',
        expectedConcurrencyToken: 'BBBBBBBBB9E=',
      });

      expect(mockClient.post).toHaveBeenCalledWith('/api/clients/client-2/restore', {
        restoredStatus: 'Lead',
        expectedConcurrencyToken: 'BBBBBBBBB9E=',
      });
    });
  });

  describe('API contract verification', () => {
    it('should use correct HTTP methods for operations', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockClients[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);
      mockClient.post.mockResolvedValueOnce(mockClients[0]);
      mockClient.put.mockResolvedValueOnce(mockClients[0]);

      await clientsApi.listClients();
      expect(mockClient.get).toHaveBeenCalled();

      await clientsApi.createClient({
        name: 'Test',
        ownerUserId: 'user-1',
        primaryContactName: 'Test',
        primaryEmail: 'test@test.com',
        primaryPhone: '555-0000',
      });
      expect(mockClient.post).toHaveBeenCalled();

      await clientsApi.updateClient('client-1', { name: 'Updated' });
      expect(mockClient.put).toHaveBeenCalled();

      mockClient.post.mockResolvedValueOnce(mockClients[0]);
      await clientsApi.archiveClient('client-1', { expectedConcurrencyToken: 'AAAAAAAAB9E=' });
      expect(mockClient.post).toHaveBeenCalledWith('/api/clients/client-1/archive', expect.any(Object));
    });

    it('should use consistent API routes', async () => {
      mockClient.get.mockResolvedValueOnce({ pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0, items: [] });
      mockClient.post.mockResolvedValueOnce(mockClients[0]);
      mockClient.put.mockResolvedValueOnce(mockClients[0]);

      await clientsApi.listClients();
      expect(mockClient.get).toHaveBeenCalledWith(expect.stringContaining('/api/clients'));

      await clientsApi.getClient('client-1');
      expect(mockClient.get).toHaveBeenCalledWith(expect.stringContaining('/api/clients/client-1'), undefined);

      await clientsApi.createClient({
        name: 'Test',
        ownerUserId: 'user-1',
        primaryContactName: 'Test',
        primaryEmail: 'test@test.com',
        primaryPhone: '555-0000',
      });
      expect(mockClient.post).toHaveBeenCalledWith(expect.stringContaining('/api/clients'), expect.any(Object));

      await clientsApi.updateClient('client-1', { name: 'Updated' });
      expect(mockClient.put).toHaveBeenCalledWith(expect.stringContaining('/api/clients/client-1'), expect.any(Object));

      mockClient.post.mockResolvedValueOnce(mockClients[0]);
      await clientsApi.archiveClient('client-1', { expectedConcurrencyToken: 'AAAAAAAAB9E=' });
      expect(mockClient.post).toHaveBeenCalledWith(
        expect.stringContaining('/api/clients/client-1/archive'),
        expect.any(Object),
      );

      mockClient.post.mockResolvedValueOnce({ ...mockClients[0], lifecycleStatus: 'Active' });
      await clientsApi.restoreClient('client-1', {
        restoredStatus: 'Active',
        expectedConcurrencyToken: 'AAAAAAAAB9E=',
      });
      expect(mockClient.post).toHaveBeenCalledWith(
        expect.stringContaining('/api/clients/client-1/restore'),
        expect.any(Object),
      );
    });
  });
});
