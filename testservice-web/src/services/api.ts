import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';
import type { Schema, Environment, Entity, User } from '../types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

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
          window.location.href = '/login';
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

  // Users
  async getUsers() {
    const response = await this.api.get('/api/users');
    return response.data;
  }

  async createUser(userData: Partial<User>) {
    const response = await this.api.post('/api/users', userData);
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

  // Generic request method
  async request<T = unknown>(config: AxiosRequestConfig): Promise<T> {
    const response = await this.api.request<T>(config);
    return response.data;
  }
}

export const apiService = new ApiService();
export default apiService;
