import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { apiService } from '../services/api';
import { normalizePermissions, normalizeRole } from '../utils/permissions';

interface User {
  id: string;
  username: string;
  email: string;
  role: string;
  permissions: string[];
}

interface AuthContextType {
  user: User | null;
  token: string | null;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
  hasPermission: (permission: string) => boolean;
  isAuthenticated: boolean;
  isLoading: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const restoreSession = async () => {
      // Check if user is already logged in
      const storedToken = localStorage.getItem('token');
      const storedUser = localStorage.getItem('user');

      if (!storedToken) {
        setIsLoading(false);
        return;
      }

      setToken(storedToken);

      // Hydrate from local cache first so UI can render quickly while /me refreshes.
      if (storedUser) {
        try {
          const parsed = JSON.parse(storedUser);
          setUser({
            ...parsed,
            role: normalizeRole(parsed.role),
            permissions: normalizePermissions(parsed.permissions, parsed.role),
          });
        } catch {
          localStorage.removeItem('user');
        }
      }

      // Refresh current user from API to avoid stale role/permission cache after redeploys.
      try {
        const currentUser = await apiService.getCurrentUser();
        const refreshedUser = {
          id: currentUser.id ?? '',
          username: currentUser.username ?? '',
          email: currentUser.email ?? '',
          role: normalizeRole(currentUser.role),
          permissions: normalizePermissions(currentUser.permissions, currentUser.role),
        };
        setUser(refreshedUser);
        localStorage.setItem('user', JSON.stringify(refreshedUser));
      } catch {
        // Token is no longer valid (or backend unavailable) - clear session.
        setUser(null);
        setToken(null);
        localStorage.removeItem('user');
        localStorage.removeItem('token');
      } finally {
        setIsLoading(false);
      }
    };

    restoreSession();
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
        role: normalizeRole(response.role),
        permissions: normalizePermissions(response.permissions, response.role),
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

  const hasPermission = (permission: string) => {
    if (!user) return false;
    return user.permissions.includes(permission);
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        login,
        logout,
        hasPermission,
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
