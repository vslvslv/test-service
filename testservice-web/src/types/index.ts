// Shared TypeScript types for the application

export interface SchemaField {
  name: string;
  type: string;
  required: boolean;
  isUnique?: boolean;
  defaultValue?: string;
}

export interface Schema {
  entityName: string;
  fields: SchemaField[];
  filterableFields?: string[];
  uniqueFields?: string[];
  useCompoundUnique?: boolean;
  excludeOnFetch: boolean;
  createdAt?: string;
}

export interface Entity {
  id: string;
  entityType: string;
  fields: Record<string, unknown>;
  isConsumed: boolean;
  environment?: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface Environment {
  id: string;
  name: string;
  description?: string;
  createdAt?: string;
}

export interface User {
  id: string;
  username: string;
  email?: string;
  role?: string;
  isActive?: boolean;
  permissions?: string[];
  customPermissions?: string[];
  firstName?: string;
  lastName?: string;
  createdAt?: string;
  lastLoginAt?: string;
}

export interface PermissionDescriptor {
  key: string;
  description: string;
  group: string;
}

export type PathMatchType = 'Exact' | 'Prefix' | 'Regex';
export type BodyMatchType = 'Any' | 'Exact' | 'Contains' | 'Regex';

export interface MockTimes {
  unlimited: boolean;
  remaining: number;
}

export interface MockRequestMatcher {
  method?: string;
  path: string;
  pathMatchType: PathMatchType;
  query: Record<string, string>;
  headers: Record<string, string>;
  body?: string;
  bodyMatchType: BodyMatchType;
}

export interface MockResponseTemplate {
  status: number;
  headers: Record<string, string>;
  body?: string;
  delayMs: number;
}

export interface MockExpectation {
  id?: string;
  environment: string;
  name: string;
  priority: number;
  enabled: boolean;
  requestMatcher: MockRequestMatcher;
  responseTemplate: MockResponseTemplate;
  times: MockTimes;
  createdAt?: string;
  updatedAt?: string;
}

export interface MockRequestLog {
  id: string;
  environment: string;
  method: string;
  path: string;
  queryString: string;
  headers: Record<string, string>;
  body?: string;
  matched: boolean;
  matchedExpectationId?: string;
  matchedExpectationName?: string;
  responseStatusCode: number;
  timestamp: string;
}

export interface MockVerificationRequest {
  environment: string;
  matcher: MockRequestMatcher;
  exactCount?: number;
  minCount?: number;
  maxCount?: number;
}

export interface MockVerificationResponse {
  success: boolean;
  matchedCount: number;
  message: string;
}

export interface LoginCredentials {
  username: string;
  password: string;
}

export interface ApiError {
  message: string;
  status?: number;
  errors?: Record<string, string[]>;
  response?: {
    data?: {
      message?: string;
    };
  };
}

// Activity types
export interface Activity {
  id: string;
  timestamp: string;
  type: 'entity' | 'schema' | 'user' | 'environment' | 'system';
  action: 'created' | 'updated' | 'deleted' | 'consumed' | 'reset' | 'bulk-reset' | 'logged-in' | 'logged-out';
  entityType?: string;
  entityId?: string;
  user: string;
  environment?: string;
  details?: ActivityDetails;
  description: string;
}

export interface ActivityDetails {
  count?: number;
  fields?: string[];
  oldValue?: string;
  newValue?: string;
  ipAddress?: string;
  userAgent?: string;
}

export interface ActivityListResponse {
  activities: Activity[];
  totalCount: number;
  skip: number;
  limit: number;
  hasMore: boolean;
}

export interface ActivityStats {
  totalActivities: number;
  startDate: string;
  endDate: string;
  byType: Record<string, number>;
  byAction: Record<string, number>;
  byEntityType: Record<string, number>;
  byUser: Record<string, number>;
}

export interface ActivityFilters {
  startDate?: string;
  endDate?: string;
  entityType?: string;
  type?: string;
  action?: string;
  user?: string;
}

// Type guard to check if error is ApiError
export function isApiError(error: unknown): error is ApiError {
  return typeof error === 'object' && error !== null && 'message' in error;
}

// Helper to get error message from unknown error (supports Axios and API { message } responses)
export function getErrorMessage(error: unknown): string {
  if (isApiError(error)) {
    const msg = error.response?.data?.message ?? error.message;
    if (typeof msg === 'string' && msg) return msg;
  }
  // Axios: error.response.data may be { message: string } or a plain string
  const err = error as { response?: { data?: unknown }; message?: string };
  if (err?.response?.data != null) {
    const data = err.response.data;
    if (typeof data === 'object' && data !== null && 'message' in data && typeof (data as { message: unknown }).message === 'string') {
      return (data as { message: string }).message;
    }
    if (typeof data === 'string' && data) return data;
  }
  if (err?.message) return err.message;
  return 'An unknown error occurred';
}
