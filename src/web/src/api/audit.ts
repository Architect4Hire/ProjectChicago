import { getGatewayClient } from './gateway';
import type { RequestOptions } from './http';

/**
 * Who/what performed an audited action. Mirrors the backend's ActorType field values.
 */
export type AuditActorType = 'User' | 'System' | 'Service' | 'Anonymous';

/**
 * One audit trail entry for a business entity (Client, Project, Task, ...).
 * Mirrors the backend's AuditEntryResult (ProjectChicago.Audit.Core.Models). Excludes the raw
 * event payload - this is the safe, display-oriented projection (ACTIVITY-001..003, AUDIT-001..008).
 */
export interface AuditEntry {
  auditEntryId: string;
  entityType: string;
  entityId: string;
  action: string;
  actionCategory: string;
  actorUserId?: string | null;
  actorType: AuditActorType;
  actorDisplayName?: string | null;
  sourceService: string;
  occurredAtUtc: string;
  auditedAtUtc: string;
  traceId: string;
  correlationId: string;
  causationId?: string | null;
  /** JSON array of changed field names only - never values. */
  changedFields: string;
  /** JSON object of previous values for safe fields only; absent when not applicable. */
  previousValues?: string | null;
  /** JSON object of new values for safe fields only; absent when not applicable. */
  newValues?: string | null;
  summaryDescription?: string | null;
}

/**
 * Paginated audit entry list. Mirrors the backend's AuditListResult.
 */
export interface AuditListResult {
  items: AuditEntry[];
  totalCount: number;
}

export interface AuditEntriesByEntityOptions {
  pageNumber?: number;
  pageSize?: number;
}

/**
 * Audit Service API operations, reached through the gateway's /api/audit route (ADR-0016/0017).
 * Read-only; restricted server-side to the Audit.Read policy (Administrator/Manager roles).
 */
export const auditApi = {
  /**
   * List audit entries for one business entity, most recent first.
   * GET /api/audit/entries-by-entity
   * ACTIVITY-001..003, AUDIT-001..008: source for a Client/Project/Task's recent activity and
   * audit history. Requires Audit.Read authorization; callers should expect a 403 for users
   * without the Administrator/Manager role.
   */
  async getEntriesByEntity(
    entityType: string,
    entityId: string,
    listOptions?: AuditEntriesByEntityOptions,
    requestOptions?: RequestOptions,
  ): Promise<AuditListResult> {
    const client = getGatewayClient();

    const params = new URLSearchParams({ entityType, entityId });
    if (listOptions?.pageNumber) params.append('pageNumber', listOptions.pageNumber.toString());
    if (listOptions?.pageSize) params.append('pageSize', listOptions.pageSize.toString());

    return client.get<AuditListResult>(`/api/audit/entries-by-entity?${params.toString()}`, requestOptions);
  },
};
