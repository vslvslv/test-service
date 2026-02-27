import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { apiService } from '../services/api';

interface User {
  id: string;
  username: string;
  email: string;
  role: string;
}

interface AuthContextType {
  user: User | null;
  token: string | null;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
  isAuthenticated: boolean;
  isLoading: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    // Check if user is already logged in
    const storedToken = localStorage.getItem('token');
    const storedUser = localStorage.getItem('user');
    
    if (storedToken && storedUser) {
      setToken(storedToken);
      setUser(JSON.parse(storedUser));
    }
    
    setIsLoading(false);
  }, []);

  // Sync session invalidation: when any tab gets 401 or token is removed in another tab, clear auth state
  useEffect(() => {
    const clearSession = () => {
      setUser(null);
      setToken(null);
      localStorage.removeItem('user');
      localStorage.removeItem('token');
    };

    const handleAuth401 = () => {
      clearSession();
    };

    const handleStorage = (e: StorageEvent) => {
      if (e.key === 'token' && e.newValue === null) {
        clearSession();
        window.dispatchEvent(new CustomEvent('auth-401'));
      }
    };

    // Detect same-tab token removal (e.g. user deleted in DevTools) - storage event only fires in other tabs
    const syncTokenWithStorage = () => {
      if (token !== null && typeof localStorage !== 'undefined' && localStorage.getItem('token') === null) {
        clearSession();
      }
    };
    const intervalId = setInterval(syncTokenWithStorage, 1000);

    window.addEventListener('auth-401', handleAuth401);
    window.addEventListener('storage', handleStorage);
    return () => {
      window.removeEventListener('auth-401', handleAuth401);
      window.removeEventListener('storage', handleStorage);
      clearInterval(intervalId);
    };
  }, [token]);

  const login = async (username: string, password: string) => {
    try {
      const response = await apiService.login(username, password);
      
      // API returns: { token, username, email, role, expiresAt }
      const userData = {
        id: '', // We don't get ID from login response
        username: response.username,
        email: response.email,
        role: response.role,
      };
      
      setToken(response.token);
      setUser(userData);
      
      localStorage.setItem('token', response.token);
      localStorage.setItem('user', JSON.stringify(userData));
    } catch (error) {
      console.error('Login failed:', error);
      throw error;
    }
  };

  const logout = () => {
    setUser(null);
    setToken(null);
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    apiService.logout();
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        login,
        logout,
        isAuthenticated: !!token,
        isLoading,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
