export {
  HttpClient,
  HttpError,
  AuthenticationError,
  AuthorizationError,
  ValidationError,
  NotFoundError,
  ConflictError,
  createGatewayClient,
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
  type ClientLifecycleStatus,
  type ClientListFilter,
  type ClientListOptions,
  type ClientSortBy,
  type CreateClientRequest,
  type PagedResponse,
  type SortDirection,
  type UpdateClientRequest,
} from './clients';
