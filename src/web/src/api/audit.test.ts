import { describe, it, expect, beforeEach, vi } from 'vitest';
import { auditApi } from './audit';
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

describe('auditApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (gatewayModule.getGatewayClient as any).mockReturnValue(mockClient);
  });

  describe('getEntriesByEntity', () => {
    it('requests entries-by-entity with required entityType/entityId query parameters', async () => {
      mockClient.get.mockResolvedValueOnce({ items: [], totalCount: 0 });

      await auditApi.getEntriesByEntity('Client', 'client-1');

      const [url] = mockClient.get.mock.calls[0];
      expect(url).toContain('/api/audit/entries-by-entity');
      expect(url).toContain('entityType=Client');
      expect(url).toContain('entityId=client-1');
    });

    it('includes pageNumber/pageSize only when provided', async () => {
      mockClient.get.mockResolvedValueOnce({ items: [], totalCount: 0 });

      await auditApi.getEntriesByEntity('Client', 'client-1', { pageNumber: 2, pageSize: 10 });

      const [url] = mockClient.get.mock.calls[0];
      expect(url).toContain('pageNumber=2');
      expect(url).toContain('pageSize=10');
    });

    it('forwards request options (e.g. an abort signal) to the gateway client', async () => {
      const controller = new AbortController();
      mockClient.get.mockResolvedValueOnce({ items: [], totalCount: 0 });

      await auditApi.getEntriesByEntity('Client', 'client-1', undefined, { signal: controller.signal });

      const [, options] = mockClient.get.mock.calls[0];
      expect(options).toEqual({ signal: controller.signal });
    });

    it('returns the paginated audit list result', async () => {
      const result = { items: [{ auditEntryId: 'a1' }], totalCount: 1 };
      mockClient.get.mockResolvedValueOnce(result);

      const response = await auditApi.getEntriesByEntity('Client', 'client-1');

      expect(response).toEqual(result);
    });
  });
});
