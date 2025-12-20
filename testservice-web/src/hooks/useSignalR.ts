import { useEffect, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

export function useSignalR<T = any>(
  onMessage: (data: T) => void,
  eventName: string
) {
  const [isConnected, setIsConnected] = useState(false);
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);

  useEffect(() => {
    const token = localStorage.getItem('token');
    if (!token) {
      console.warn('No auth token found, skipping SignalR connection');
      return;
    }

    // Create SignalR connection
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/notificationHub`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    setConnection(newConnection);

    return () => {
      if (newConnection.state === signalR.HubConnectionState.Connected) {
        newConnection.stop();
      }
    };
  }, []);

  useEffect(() => {
    if (!connection) return;

    const startConnection = async () => {
      try {
        await connection.start();
        console.log(`✅ SignalR connected for event: ${eventName}`);
        setIsConnected(true);
      } catch (err) {
        console.error('SignalR connection error:', err);
        setIsConnected(false);
      }
    };

    // Set up event handler
    connection.on(eventName, (data: T) => {
      console.log(`📨 SignalR event received: ${eventName}`, data);
      onMessage(data);
    });

    // Set up reconnection handlers
    connection.onreconnecting(() => {
      console.log('🔄 SignalR reconnecting...');
      setIsConnected(false);
    });

    connection.onreconnected(() => {
      console.log('✅ SignalR reconnected');
      setIsConnected(true);
    });

    connection.onclose(() => {
      console.log('❌ SignalR connection closed');
      setIsConnected(false);
    });

    // Start connection if not already started
    if (connection.state === signalR.HubConnectionState.Disconnected) {
      startConnection();
    }

    return () => {
      connection.off(eventName);
    };
  }, [connection, eventName, onMessage]);

  const disconnect = useCallback(() => {
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
      connection.stop();
    }
  }, [connection]);

  return { isConnected, disconnect };
}
