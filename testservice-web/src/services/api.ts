import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';
import type {
  Schema,
  Environment,
  Entity,
  User,
  Activity,
  ActivityListResponse,
  ActivityStats,
  ActivityFilters,
  PermissionDescriptor,
  MockExpectation,
  MockRequestLog,
  MockVerificationRequest,
  MockVerificationResponse
} from '../types';

// For development: use /api (proxied). For production: use env or relative path so same host is used (no localhost).
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? (import.meta.env.DEV ? '/api' : '/testservice');

// Log API configuration for debugging
if (typeof window !== 'undefined') {
  console.log('🔧 API Configuration:', {
    BASE_URL: API_BASE_URL,
    DEV: import.meta.env.DEV,
    MODE: import.meta.env.MODE,
    VITE_API_BASE_URL: import.meta.env.VITE_API_BASE_URL,
  });
}

class ApiService {
  private api: AxiosInstance;

  constructor() {
    this.api = axios.create({
      baseURL: API_BASE_URL,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    // Request interceptor to add auth token
    this.api.interceptors.request.use(
      (config) => {
        const token = localStorage.getItem('token');
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    // Response interceptor for error handling
    this.api.interceptors.response.use(
      (response) => response,
      (error) => {
        if (error.response?.status === 401) {
          localStorage.removeItem('token');
          // Dispatch custom event for navigation - components will handle the redirect
          window.dispatchEvent(new CustomEvent('auth-401'));
        }
        return Promise.reject(error);
      }
    );
  }

  // Authentication
  async login(username: string, password: string) {
    const response = await this.api.post('/api/auth/login', { 
      Username: username, 
      Password: password 
    });
    if (response.data.token) {
      localStorage.setItem('token', response.data.token);
    }
    return response.data;
  }

  async logout() {
    localStorage.removeItem('token');
  }

  async getCurrentUser() {
    const response = await this.api.get('/api/auth/me');
    return response.data;
  }

  // Users
  async getUsers() {
    const response = await this.api.get('/api/users');
    return response.data;
  }

  async createUser(userData: Partial<User>) {
    const response = await this.api.post('/api/users', userData);
    return response.data;
  }

  async updateUser(id: string, userData: Partial<User>) {
    const response = await this.api.put(`/api/users/${id}`, userData);
    return response.data;
  }

  async deleteUser(id: string) {
    const response = await this.api.delete(`/api/users/${id}`);
    return response.data;
  }

  async getPermissionsCatalog(): Promise<{ permissions: PermissionDescriptor[]; roleDefaults: Record<string, string[]> }> {
    const response = await this.api.get('/api/users/permissions/catalog');
    return response.data;
  }

  // Mocks
  async getMockExpectations(environment?: string, includeDisabled: boolean = false): Promise<MockExpectation[]> {
    const response = await this.api.get<MockExpectation[]>('/api/mocks/expectations', {
      params: { environment, includeDisabled }
    });
    return response.data;
  }

  async createMockExpectation(expectation: MockExpectation): Promise<MockExpectation> {
    const response = await this.api.post<MockExpectation>('/api/mocks/expectations', expectation);
    return response.data;
  }

  async updateMockExpectation(id: string, expectation: MockExpectation): Promise<void> {
    await this.api.put(`/api/mocks/expectations/${id}`, expectation);
  }

  async deleteMockExpectation(id: string): Promise<void> {
    await this.api.delete(`/api/mocks/expectations/${id}`);
  }

  async getMockRequestLogs(environment?: string, path?: string, limit: number = 100, matched?: boolean): Promise<MockRequestLog[]> {
    const response = await this.api.get<MockRequestLog[]>('/api/mocks/requests', {
      params: { environment, path, limit, matched }
    });
    return response.data;
  }

  async deleteMockRequestLogs(environment?: string): Promise<{ deletedCount: number }> {
    const response = await this.api.delete<{ deletedCount: number }>('/api/mocks/requests', {
      params: { environment }
    });
    return response.data;
  }

  async verifyMockRequests(request: MockVerificationRequest): Promise<MockVerificationResponse> {
    const response = await this.api.post<MockVerificationResponse>('/api/mocks/verify', request);
    return response.data;
  }

  // Environments
  async getEnvironments(): Promise<Environment[]> {
    const response = await this.api.get<Environment[]>('/api/environments');
    return response.data;
  }

  async createEnvironment(envData: Partial<Environment>) {
    const response = await this.api.post('/api/environments', envData);
    return response.data;
  }

  async updateEnvironment(id: string, envData: { displayName?: string; description?: string; url?: string; color?: string; isActive?: boolean; order?: number }) {
    const response = await this.api.put(`/api/environments/${id}`, envData);
    return response.data;
  }

  // Schemas
  async getSchemas(): Promise<Schema[]> {
    const response = await this.api.get<Schema[]>('/api/schemas');
    return response.data;
  }

  async createSchema(schemaData: Partial<Schema>) {
    const response = await this.api.post('/api/schemas', schemaData);
    return response.data;
  }

  async getSchema(name: string): Promise<Schema> {
    const response = await this.api.get<Schema>(`/api/schemas/${name}`);
    return response.data;
  }

  async updateSchema(name: string, schemaData: Partial<Schema>) {
    const response = await this.api.put(`/api/schemas/${name}`, schemaData);
    return response.data;
  }

  async deleteSchema(name: string) {
    const response = await this.api.delete(`/api/schemas/${name}`);
    return response.data;
  }

  async deleteAllSchemaEntities(schemaName: string, environment?: string) {
    const params = environment ? { environment } : {};
    const response = await this.api.delete(`/api/schemas/${schemaName}/entities`, { params });
    return response.data;
  }

  // Dynamic Entities
  async getEntities(entityType: string, environment?: string): Promise<Entity[]> {
    const params = environment ? { environment } : {};
    const response = await this.api.get<Entity[]>(`/api/entities/${entityType}`, { params });
    return response.data;
  }

  async getEntity(entityType: string, id: string): Promise<Entity> {
    const response = await this.api.get<Entity>(`/api/entities/${entityType}/${id}`);
    return response.data;
  }

  async createEntity(entityType: string, entityData: Record<string, unknown>) {
    const response = await this.api.post(`/api/entities/${entityType}`, entityData);
    return response.data;
  }

  async updateEntity(entityType: string, id: string, entityData: Record<string, unknown>) {
    const response = await this.api.put(`/api/entities/${entityType}/${id}`, entityData);
    return response.data;
  }

  async deleteEntity(entityType: string, id: string) {
    const response = await this.api.delete(`/api/entities/${entityType}/${id}`);
    return response.data;
  }

  async getNextAvailable(entityType: string, environment?: string): Promise<Entity> {
    const params = environment ? { environment } : {};
    const response = await this.api.get<Entity>(`/api/entities/${entityType}/next`, { params });
    return response.data;
  }

  // Settings
  async getSettings() {
    const response = await this.api.get('/api/settings');
    return response.data;
  }

  async updateSettings(settings: any) {
    const response = await this.api.put('/api/settings', settings);
    return response.data;
  }

  // API Keys
  async getApiKeys() {
    const response = await this.api.get('/api/settings/api-keys');
    return response.data;
  }

  async createApiKey(keyData: { name: string; expirationDays: number | null }) {
    const response = await this.api.post('/api/settings/api-keys', keyData);
    return response.data;
  }

  async deleteApiKey(id: string) {
    const response = await this.api.delete(`/api/settings/api-keys/${id}`);
    return response.data;
  }

  // Activities
  async getActivities(filters: ActivityFilters & { skip?: number; limit?: number } = {}): Promise<ActivityListResponse> {
    const response = await this.api.get<ActivityListResponse>('/api/activities', { params: filters });
    return response.data;
  }

  async getRecentActivities(hours: number = 24, limit: number = 100): Promise<Activity[]> {
    const response = await this.api.get<Activity[]>('/api/activities/recent', { 
      params: { hours, limit } 
    });
    return response.data;
  }

  async getActivityStats(startDate?: string, endDate?: string): Promise<ActivityStats> {
    const response = await this.api.get<ActivityStats>('/api/activities/stats', {
      params: { startDate, endDate }
    });
    return response.data;
  }

  // Generic request method
  async request<T = unknown>(config: AxiosRequestConfig): Promise<T> {
    const response = await this.api.request<T>(config);
    return response.data;
  }
}

export const apiService = new ApiService();
export default apiService;
