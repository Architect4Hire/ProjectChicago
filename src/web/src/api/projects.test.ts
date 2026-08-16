import { describe, it, expect, beforeEach, vi } from 'vitest';
import { projectsApi } from './projects';
import * as gatewayModule from './gateway';

vi.mock('./gateway', () => ({
  getGatewayClient: vi.fn(),
}));

const mockClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
};

const mockProjects = [
  {
    id: 'project-1',
    clientId: 'client-1',
    clientName: 'Acme Corp',
    name: 'Website Redesign',
    description: 'Complete redesign of company website',
    status: 'Active' as const,
    priority: 'High' as const,
    ownerUserId: 'user-1',
    startDateUtc: '2024-01-15T00:00:00Z',
    targetCompletionDateUtc: '2024-06-30T00:00:00Z',
    actualCompletionDateUtc: null,
    notes: 'High priority client deliverable',
    createdAtUtc: '2024-01-01T00:00:00Z',
    createdBy: 'user-1',
    lastModifiedAtUtc: '2024-08-15T10:30:00Z',
    lastModifiedBy: 'user-1',
  },
  {
    id: 'project-2',
    clientId: 'client-2',
    clientName: 'TechStart Inc',
    name: 'Mobile App Development',
    description: 'iOS and Android mobile application',
    status: 'Planned' as const,
    priority: 'Normal' as const,
    ownerUserId: 'user-2',
    startDateUtc: '2024-09-01T00:00:00Z',
    targetCompletionDateUtc: '2024-12-31T00:00:00Z',
    actualCompletionDateUtc: null,
    notes: 'Planned for Q4 2024',
    createdAtUtc: '2024-08-01T00:00:00Z',
    createdBy: 'user-2',
    lastModifiedAtUtc: '2024-08-15T09:15:00Z',
    lastModifiedBy: 'user-2',
  },
];

describe('projectsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (gatewayModule.getGatewayClient as any).mockReturnValue(mockClient);
  });

  describe('listProjects', () => {
    it('should fetch projects list without options', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 2,
        totalPages: 1,
        items: mockProjects,
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      const result = await projectsApi.listProjects();

      expect(mockClient.get).toHaveBeenCalledWith('/api/projects');
      expect(result).toEqual(mockResponse);
      expect(result.items).toHaveLength(2);
    });

    it('should fetch projects list with pagination options', async () => {
      const mockResponse = {
        pageNumber: 2,
        pageSize: 10,
        totalCount: 15,
        totalPages: 2,
        items: [mockProjects[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      const result = await projectsApi.listProjects({
        pageNumber: 2,
        pageSize: 10,
      });

      expect(mockClient.get).toHaveBeenCalledWith(
        expect.stringContaining('/api/projects?')
      );
      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('pageNumber=2');
      expect(callArg).toContain('pageSize=10');
      expect(result.pageNumber).toBe(2);
    });

    it('should fetch projects list with search parameter', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockProjects[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      const result = await projectsApi.listProjects({
        search: 'Website',
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('search=Website');
      expect(result.items).toHaveLength(1);
    });

    it('should fetch projects list filtered by client ID', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockProjects[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await projectsApi.listProjects({
        clientId: 'client-1',
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('clientId=client-1');
    });

    it('should fetch projects list with status filter', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockProjects[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await projectsApi.listProjects({
        status: 'Active',
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('status=Active');
    });

    it('should fetch projects list with multiple status filters', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 2,
        totalPages: 1,
        items: mockProjects,
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await projectsApi.listProjects({
        status: ['Active', 'Planned'],
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('status=Active');
      expect(callArg).toContain('status=Planned');
    });

    it('should fetch projects list filtered by owner', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockProjects[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await projectsApi.listProjects({
        ownerUserId: 'user-1',
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('ownerUserId=user-1');
    });

    it('should fetch projects list with priority filter', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockProjects[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await projectsApi.listProjects({
        priority: 'High',
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('priority=High');
    });

    it('should fetch projects list with multiple priority filters', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 2,
        totalPages: 1,
        items: mockProjects,
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await projectsApi.listProjects({
        priority: ['High', 'Normal'],
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('priority=High');
      expect(callArg).toContain('priority=Normal');
    });

    it('should fetch projects list with date range filters', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockProjects[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await projectsApi.listProjects({
        startDateFromUtc: '2024-01-01T00:00:00Z',
        startDateToUtc: '2024-06-30T00:00:00Z',
        targetCompletionDateFromUtc: '2024-06-01T00:00:00Z',
        targetCompletionDateToUtc: '2024-12-31T00:00:00Z',
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('startDateFromUtc=2024-01-01T00%3A00%3A00Z');
      expect(callArg).toContain('startDateToUtc=2024-06-30T00%3A00%3A00Z');
      expect(callArg).toContain('targetCompletionDateFromUtc=2024-06-01T00%3A00%3A00Z');
      expect(callArg).toContain('targetCompletionDateToUtc=2024-12-31T00%3A00%3A00Z');
    });

    it('should fetch projects list with exclude archived flag', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [mockProjects[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await projectsApi.listProjects({
        excludeArchived: true,
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('excludeArchived=true');
    });

    it('should fetch projects list with sort options', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 20,
        totalCount: 2,
        totalPages: 1,
        items: mockProjects,
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await projectsApi.listProjects({
        sortBy: 'name',
        sortDirection: 'Ascending',
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('sortBy=name');
      expect(callArg).toContain('sortDirection=Ascending');
    });

    it('should fetch projects list with combined options', async () => {
      const mockResponse = {
        pageNumber: 1,
        pageSize: 10,
        totalCount: 1,
        totalPages: 1,
        items: [mockProjects[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);

      await projectsApi.listProjects({
        pageNumber: 1,
        pageSize: 10,
        search: 'Website',
        clientId: 'client-1',
        status: 'Active',
        ownerUserId: 'user-1',
        priority: 'High',
        excludeArchived: true,
        sortBy: 'name',
        sortDirection: 'Descending',
      });

      const callArg = (mockClient.get as any).mock.calls[0][0] as string;
      expect(callArg).toContain('pageNumber=1');
      expect(callArg).toContain('pageSize=10');
      expect(callArg).toContain('search=Website');
      expect(callArg).toContain('clientId=client-1');
      expect(callArg).toContain('status=Active');
      expect(callArg).toContain('ownerUserId=user-1');
      expect(callArg).toContain('priority=High');
      expect(callArg).toContain('excludeArchived=true');
      expect(callArg).toContain('sortBy=name');
      expect(callArg).toContain('sortDirection=Descending');
    });
  });

  describe('getProject', () => {
    const mockProjectDetail = {
      project: {
        id: 'project-1',
        clientId: 'client-1',
        clientName: 'Acme Corp',
        name: 'Website Redesign',
        description: 'Complete redesign of company website',
        status: 'Active' as const,
        priority: 'High' as const,
        ownerUserId: 'user-1',
        startDateUtc: '2024-01-15T00:00:00Z',
        targetCompletionDateUtc: '2024-06-30T00:00:00Z',
        actualCompletionDateUtc: null,
        notes: 'High priority client deliverable',
        createdAtUtc: '2024-01-01T00:00:00Z',
        createdBy: 'user-1',
        lastModifiedAtUtc: '2024-08-15T10:30:00Z',
        lastModifiedBy: 'user-1',
        concurrencyToken: 'AAAAAAAAB9E=',
      },
      openTasks: [],
      completedTasks: [],
    };

    it('should fetch the consolidated Project detail by ID (PROJECT-030)', async () => {
      mockClient.get.mockResolvedValueOnce(mockProjectDetail);

      const result = await projectsApi.getProject('project-1');

      expect(mockClient.get).toHaveBeenCalledWith('/api/projects/project-1', undefined);
      expect(result).toEqual(mockProjectDetail);
      expect(result.project.id).toBe('project-1');
    });

    it('forwards request options (e.g. an abort signal) to the gateway client', async () => {
      const controller = new AbortController();
      mockClient.get.mockResolvedValueOnce(mockProjectDetail);

      await projectsApi.getProject('project-1', { signal: controller.signal });

      expect(mockClient.get).toHaveBeenCalledWith('/api/projects/project-1', { signal: controller.signal });
    });
  });

  describe('createProject', () => {
    it('should create a new project with required fields', async () => {
      const createRequest = {
        clientId: 'client-1',
        name: 'New Project',
        ownerUserId: 'user-1',
      };

      const createdProject = {
        ...mockProjects[0],
        ...createRequest,
        id: 'project-new',
        createdAtUtc: '2024-08-15T12:00:00Z',
        createdBy: 'current-user',
      };

      mockClient.post.mockResolvedValueOnce(createdProject);

      const result = await projectsApi.createProject(createRequest);

      expect(mockClient.post).toHaveBeenCalledWith('/api/projects', createRequest);
      expect(result.name).toBe('New Project');
      expect(result.id).toBe('project-new');
    });

    it('should create a project with all optional fields', async () => {
      const createRequest = {
        clientId: 'client-1',
        name: 'Full Project',
        ownerUserId: 'user-3',
        description: 'Complete project information',
        priority: 'High' as const,
        startDateUtc: '2024-09-01T00:00:00Z',
        targetCompletionDateUtc: '2024-12-31T00:00:00Z',
        notes: 'Important project notes',
      };

      const createdProject = {
        ...mockProjects[0],
        ...createRequest,
        id: 'project-full',
      };

      mockClient.post.mockResolvedValueOnce(createdProject);

      const result = await projectsApi.createProject(createRequest);

      expect(mockClient.post).toHaveBeenCalledWith('/api/projects', createRequest);
      expect(result.priority).toBe('High');
      expect(result.description).toBe('Complete project information');
    });
  });

  describe('updateProject', () => {
    it('should update a project with partial fields', async () => {
      const updateRequest = {
        name: 'Updated Project Name',
        priority: 'Critical' as const,
      };

      const updatedProject = {
        ...mockProjects[0],
        ...updateRequest,
      };

      mockClient.put.mockResolvedValueOnce(updatedProject);

      const result = await projectsApi.updateProject('project-1', updateRequest);

      expect(mockClient.put).toHaveBeenCalledWith('/api/projects/project-1', updateRequest);
      expect(result.name).toBe('Updated Project Name');
      expect(result.priority).toBe('Critical');
    });

    it('should update a project with multiple fields', async () => {
      const updateRequest = {
        description: 'Updated description',
        ownerUserId: 'user-2',
        targetCompletionDateUtc: '2024-12-31T00:00:00Z',
      };

      const updatedProject = {
        ...mockProjects[0],
        ...updateRequest,
      };

      mockClient.put.mockResolvedValueOnce(updatedProject);

      const result = await projectsApi.updateProject('project-1', updateRequest);

      expect(mockClient.put).toHaveBeenCalledWith('/api/projects/project-1', updateRequest);
      expect(result.ownerUserId).toBe('user-2');
      expect(result.description).toBe('Updated description');
    });
  });

  describe('changeStatus', () => {
    it('should change project status (PROJECT-010)', async () => {
      const changeRequest = {
        newStatus: 'Completed' as const,
        expectedConcurrencyToken: 'AAAAAAAAB9E=',
      };

      const updatedProject = {
        ...mockProjects[0],
        status: 'Completed' as const,
        actualCompletionDateUtc: '2024-08-15T12:00:00Z',
      };

      mockClient.patch.mockResolvedValueOnce(updatedProject);

      const result = await projectsApi.changeStatus('project-1', changeRequest);

      expect(mockClient.patch).toHaveBeenCalledWith('/api/projects/project-1/status', changeRequest);
      expect(result.status).toBe('Completed');
    });

    it('should acknowledge open tasks when completing project (PROJECT-013)', async () => {
      const changeRequest = {
        newStatus: 'Completed' as const,
        expectedConcurrencyToken: 'AAAAAAAAB9E=',
        acknowledgeOpenTasks: true,
      };

      const updatedProject = {
        ...mockProjects[0],
        status: 'Completed' as const,
      };

      mockClient.patch.mockResolvedValueOnce(updatedProject);

      await projectsApi.changeStatus('project-1', changeRequest);

      expect(mockClient.patch).toHaveBeenCalledWith('/api/projects/project-1/status', expect.objectContaining({
        acknowledgeOpenTasks: true,
      }));
    });

    it('should capture actual completion date when completing (PROJECT-012)', async () => {
      const changeRequest = {
        newStatus: 'Completed' as const,
        expectedConcurrencyToken: 'AAAAAAAAB9E=',
      };

      const completedProject = {
        ...mockProjects[0],
        status: 'Completed' as const,
        actualCompletionDateUtc: '2024-08-16T10:30:00Z',
      };

      mockClient.patch.mockResolvedValueOnce(completedProject);

      const result = await projectsApi.changeStatus('project-1', changeRequest);

      expect(result.actualCompletionDateUtc).not.toBeNull();
    });
  });

  describe('archiveProject', () => {
    it('should archive a project by ID with concurrency token (PROJECT-014)', async () => {
      const archivedProject = { ...mockProjects[0], status: 'Archived' as const };
      mockClient.post.mockResolvedValueOnce(archivedProject);

      const result = await projectsApi.archiveProject('project-1', { expectedConcurrencyToken: 'AAAAAAAAB9E=' });

      expect(mockClient.post).toHaveBeenCalledWith('/api/projects/project-1/archive', {
        expectedConcurrencyToken: 'AAAAAAAAB9E=',
      });
      expect(result.status).toBe('Archived');
    });

    it('should archive different projects', async () => {
      mockClient.post.mockResolvedValueOnce(mockProjects[1]);

      await projectsApi.archiveProject('project-2', { expectedConcurrencyToken: 'BBBBBBBBB9E=' });

      expect(mockClient.post).toHaveBeenCalledWith('/api/projects/project-2/archive', {
        expectedConcurrencyToken: 'BBBBBBBBB9E=',
      });
    });
  });

  describe('restoreProject', () => {
    it('should restore an archived project to a chosen status (PROJECT-014)', async () => {
      const restoredProject = { ...mockProjects[0], status: 'Active' as const };
      mockClient.post.mockResolvedValueOnce(restoredProject);

      const result = await projectsApi.restoreProject('project-1', {
        restoredStatus: 'Active',
        expectedConcurrencyToken: 'AAAAAAAAB9E=',
      });

      expect(mockClient.post).toHaveBeenCalledWith('/api/projects/project-1/restore', {
        restoredStatus: 'Active',
        expectedConcurrencyToken: 'AAAAAAAAB9E=',
      });
      expect(result.status).toBe('Active');
    });

    it('should restore different projects to different statuses', async () => {
      mockClient.post.mockResolvedValueOnce({ ...mockProjects[1], status: 'Planned' as const });

      await projectsApi.restoreProject('project-2', {
        restoredStatus: 'Planned',
        expectedConcurrencyToken: 'BBBBBBBBB9E=',
      });

      expect(mockClient.post).toHaveBeenCalledWith('/api/projects/project-2/restore', {
        restoredStatus: 'Planned',
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
        items: [mockProjects[0]],
      };

      mockClient.get.mockResolvedValueOnce(mockResponse);
      mockClient.post.mockResolvedValueOnce(mockProjects[0]);
      mockClient.put.mockResolvedValueOnce(mockProjects[0]);
      mockClient.patch.mockResolvedValueOnce(mockProjects[0]);

      await projectsApi.listProjects();
      expect(mockClient.get).toHaveBeenCalled();

      await projectsApi.createProject({
        clientId: 'client-1',
        name: 'Test',
        ownerUserId: 'user-1',
      });
      expect(mockClient.post).toHaveBeenCalled();

      await projectsApi.updateProject('project-1', { name: 'Updated' });
      expect(mockClient.put).toHaveBeenCalled();

      mockClient.patch.mockResolvedValueOnce(mockProjects[0]);
      await projectsApi.changeStatus('project-1', {
        newStatus: 'Active',
        expectedConcurrencyToken: 'AAAAAAAAB9E=',
      });
      expect(mockClient.patch).toHaveBeenCalled();
    });

    it('should use consistent API routes (API-002)', async () => {
      mockClient.get.mockResolvedValueOnce({ pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0, items: [] });
      mockClient.post.mockResolvedValueOnce(mockProjects[0]);
      mockClient.put.mockResolvedValueOnce(mockProjects[0]);

      await projectsApi.listProjects();
      expect(mockClient.get).toHaveBeenCalledWith(expect.stringContaining('/api/projects'));

      await projectsApi.getProject('project-1');
      expect(mockClient.get).toHaveBeenCalledWith(expect.stringContaining('/api/projects/project-1'), undefined);

      await projectsApi.createProject({
        clientId: 'client-1',
        name: 'Test',
        ownerUserId: 'user-1',
      });
      expect(mockClient.post).toHaveBeenCalledWith(expect.stringContaining('/api/projects'), expect.any(Object));

      await projectsApi.updateProject('project-1', { name: 'Updated' });
      expect(mockClient.put).toHaveBeenCalledWith(expect.stringContaining('/api/projects/project-1'), expect.any(Object));

      mockClient.post.mockResolvedValueOnce(mockProjects[0]);
      await projectsApi.archiveProject('project-1', { expectedConcurrencyToken: 'AAAAAAAAB9E=' });
      expect(mockClient.post).toHaveBeenCalledWith(
        expect.stringContaining('/api/projects/project-1/archive'),
        expect.any(Object),
      );

      mockClient.post.mockResolvedValueOnce({ ...mockProjects[0], status: 'Planned' });
      await projectsApi.restoreProject('project-1', {
        restoredStatus: 'Planned',
        expectedConcurrencyToken: 'AAAAAAAAB9E=',
      });
      expect(mockClient.post).toHaveBeenCalledWith(
        expect.stringContaining('/api/projects/project-1/restore'),
        expect.any(Object),
      );
    });
  });
});
