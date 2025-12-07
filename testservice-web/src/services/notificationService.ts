import * as signalR from '@microsoft/signalr';

export interface Notification {
  type: 'schema_created' | 'schema_updated' | 'schema_deleted' | 'entity_created' | 'entity_updated' | 'entity_deleted';
  schemaName?: string;
  entityType?: string;
  entityId?: string;
  schema?: any;
  timestamp: string;
}

export type NotificationHandler = (notification: Notification) => void;

class NotificationService {
  private connection: signalR.HubConnection | null = null;
  private handlers: NotificationHandler[] = [];
  private reconnectTimer: NodeJS.Timeout | null = null;

  async connect() {
    const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';
    const token = localStorage.getItem('token');
    
    console.log('?? Connecting to SignalR...');
    console.log('   API URL:', API_BASE_URL);
    console.log('   Has Token:', !!token);
    
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/notificationHub`, {
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets,
        accessTokenFactory: () => {
          const token = localStorage.getItem('token');
          console.log('   Providing token to SignalR:', token ? 'Yes' : 'No');
          return token || '';
        }
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 0s, 2s, 10s, 30s...
          if (retryContext.previousRetryCount < 3) {
            return Math.pow(2, retryContext.previousRetryCount) * 1000;
          }
          return 30000; // Max 30 seconds
        }
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Set up event handlers
    this.connection.on('SchemaCreated', (data: any) => {
      console.log('?? SchemaCreated event received from SignalR:', data);
      console.log('   Raw data:', JSON.stringify(data, null, 2));
      
      // Transform backend data to frontend Notification format
      const notification: Notification = {
        type: 'schema_created',
        schemaName: data.schemaName,
        timestamp: data.timestamp,
        schema: data.schema
      };
      
      console.log('   Transformed notification:', notification);
      this.notifyHandlers(notification);
    });

    this.connection.on('SchemaUpdated', (data: any) => {
      console.log('?? SchemaUpdated event received from SignalR:', data);
      console.log('   Raw data:', JSON.stringify(data, null, 2));
      
      // Transform backend data to frontend Notification format
      const notification: Notification = {
        type: 'schema_updated',
        schemaName: data.schemaName,
        timestamp: data.timestamp,
        schema: data.schema
      };
      
      console.log('   Transformed notification:', notification);
      this.notifyHandlers(notification);
    });

    this.connection.on('SchemaDeleted', (data: any) => {
      console.log('?? SchemaDeleted event received from SignalR:', data);
      console.log('   Raw data:', JSON.stringify(data, null, 2));
      
      // Transform backend data to frontend Notification format
      const notification: Notification = {
        type: 'schema_deleted',
        schemaName: data.schemaName,
        timestamp: data.timestamp
      };
      
      console.log('   Transformed notification:', notification);
      this.notifyHandlers(notification);
    });

    this.connection.on('EntityCreated', (data: Notification) => {
      console.log('Entity created notification:', data);
      this.notifyHandlers(data);
    });

    this.connection.on('EntityUpdated', (data: Notification) => {
      console.log('Entity updated notification:', data);
      this.notifyHandlers(data);
    });

    this.connection.on('EntityDeleted', (data: Notification) => {
      console.log('Entity deleted notification:', data);
      this.notifyHandlers(data);
    });

    this.connection.onreconnecting((error) => {
      console.warn('SignalR reconnecting...', error);
    });

    this.connection.onreconnected((connectionId) => {
      console.log('SignalR reconnected:', connectionId);
    });

    this.connection.onclose((error) => {
      console.error('SignalR connection closed:', error);
      // Try to reconnect after 5 seconds
      if (this.reconnectTimer) clearTimeout(this.reconnectTimer);
      this.reconnectTimer = setTimeout(() => {
        console.log('Attempting to reconnect...');
        this.connect().catch(console.error);
      }, 5000);
    });

    try {
      await this.connection.start();
      console.log('? SignalR connected successfully');
      console.log('   Connection ID:', this.connection.connectionId);
      console.log('   Connection State:', this.connection.state);
      
      // List all registered event handlers
      console.log('   Registered event handlers:', [
        'SchemaCreated',
        'SchemaUpdated', 
        'SchemaDeleted',
        'EntityCreated',
        'EntityUpdated',
        'EntityDeleted'
      ]);
    } catch (err) {
      console.error('? SignalR connection error:', err);
      // Retry after 5 seconds
      if (this.reconnectTimer) clearTimeout(this.reconnectTimer);
      this.reconnectTimer = setTimeout(() => {
        this.connect().catch(console.error);
      }, 5000);
    }
  }

  disconnect() {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    
    if (this.connection) {
      this.connection.stop();
      this.connection = null;
    }
  }

  subscribe(handler: NotificationHandler): () => void {
    this.handlers.push(handler);
    
    // Return unsubscribe function
    return () => {
      this.handlers = this.handlers.filter(h => h !== handler);
    };
  }

  private notifyHandlers(notification: Notification) {
    this.handlers.forEach(handler => {
      try {
        handler(notification);
      } catch (err) {
        console.error('Error in notification handler:', err);
      }
    });
  }

  getConnectionState(): signalR.HubConnectionState | null {
    return this.connection?.state ?? null;
  }

  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  // Debug method to check connection status
  getDebugInfo() {
    return {
      connectionState: this.connection?.state,
      connectionId: this.connection?.connectionId,
      isConnected: this.isConnected(),
      handlerCount: this.handlers.length
    };
  }
}

export const notificationService = new NotificationService();
export default notificationService;
