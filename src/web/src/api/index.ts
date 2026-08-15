export {
  HttpClient,
  HttpError,
  AuthenticationError,
  AuthorizationError,
  ValidationError,
  NotFoundError,
  ConflictError,
  createGatewayClient,
  setCsrfToken,
  getCsrfToken,
  type HttpClientConfig,
  type RequestOptions,
  type ProblemDetails,
} from './http';

export {
  initializeGatewayClient,
  getGatewayClient,
} from './gateway';

export {
  clientsApi,
  type Client,
  type ClientDetail,
  type ClientDetailProjectSummary,
  type ClientDetailRecord,
  type ClientDetailTaskSummary,
  type ClientDuplicateMatchField,
  type ClientDuplicateWarning,
  type ClientLifecycleStatus,
  type ClientListFilter,
  type ClientListOptions,
  type ClientSortBy,
  type ArchiveClientRequest,
  type ChangeClientLifecycleStatusRequest,
  type CreateClientRequest,
  type PagedResponse,
  type Priority,
  type ProjectPriority,
  type ProjectStatus,
  type RestoreClientRequest,
  type SortDirection,
  type TaskItemPriority,
  type TaskItemStatus,
  type UpdateClientRequest,
} from './clients';

export {
  auditApi,
  type AuditActorType,
  type AuditEntry,
  type AuditListResult,
} from './audit';
