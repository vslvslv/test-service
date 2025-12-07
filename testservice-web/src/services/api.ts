import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';

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

  async createUser(userData: any) {
    const response = await this.api.post('/api/users', userData);
    return response.data;
  }

  // Environments
  async getEnvironments() {
    const response = await this.api.get('/api/environments');
    return response.data;
  }

  async createEnvironment(envData: any) {
    const response = await this.api.post('/api/environments', envData);
    return response.data;
  }

  // Schemas
  async getSchemas() {
    const response = await this.api.get('/api/schemas');
    return response.data;
  }

  async createSchema(schemaData: any) {
    const response = await this.api.post('/api/schemas', schemaData);
    return response.data;
  }

  async getSchema(name: string) {
    const response = await this.api.get(`/api/schemas/${name}`);
    return response.data;
  }

  async updateSchema(name: string, schemaData: any) {
    const response = await this.api.put(`/api/schemas/${name}`, schemaData);
    return response.data;
  }

  async deleteSchema(name: string) {
    const response = await this.api.delete(`/api/schemas/${name}`);
    return response.data;
  }

  // Dynamic Entities
  async getEntities(entityType: string, environment?: string) {
    const params = environment ? { environment } : {};
    const response = await this.api.get(`/api/entities/${entityType}`, { params });
    return response.data;
  }

  async getEntity(entityType: string, id: string) {
    const response = await this.api.get(`/api/entities/${entityType}/${id}`);
    return response.data;
  }

  async createEntity(entityType: string, entityData: any) {
    const response = await this.api.post(`/api/entities/${entityType}`, entityData);
    return response.data;
  }

  async updateEntity(entityType: string, id: string, entityData: any) {
    const response = await this.api.put(`/api/entities/${entityType}/${id}`, entityData);
    return response.data;
  }

  async deleteEntity(entityType: string, id: string) {
    const response = await this.api.delete(`/api/entities/${entityType}/${id}`);
    return response.data;
  }

  async getNextAvailable(entityType: string, environment?: string) {
    const params = environment ? { environment } : {};
    const response = await this.api.get(`/api/entities/${entityType}/next`, { params });
    return response.data;
  }

  // Generic request method
  async request<T = any>(config: AxiosRequestConfig): Promise<T> {
    const response = await this.api.request<T>(config);
    return response.data;
  }
}

export const apiService = new ApiService();
export default apiService;
