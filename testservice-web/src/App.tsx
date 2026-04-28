import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { ToastProvider, useToast } from './contexts/ToastContext';
import { useCallback, useEffect } from 'react';
import notificationService, { Notification } from './services/notificationService';
import Layout from './components/Layout';
import ProtectedRoute from './components/ProtectedRoute';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import Schemas from './pages/Schemas';
import CreateSchema from './pages/CreateSchema';
import EditSchema from './pages/EditSchema';
import Environments from './pages/Environments';
import Entities from './pages/Entities';
import EntityList from './pages/EntityList';
import Activity from './pages/Activity';
import Settings from './pages/Settings';
import Users from './pages/Users';
import Mocks from './pages/Mocks';
import { Permissions } from './utils/permissions';
import './App.css';

function AuthHandler() {
  // No-op: redirect is handled by ProtectedRoute when isAuthenticated becomes false after auth-401 or token sync
  return null;
}

function NotificationHandler() {
  const { success, info, warning, notifyBell } = useToast();
  const { isAuthenticated } = useAuth();

  const handleNotification = useCallback((notification: Notification) => {
    notifyBell(notification);

    switch (notification.type) {
      case 'schema_created':
        success('Schema Created', `Schema "${notification.schemaName}" has been created`);
        break;
      case 'schema_updated':
        info('Schema Updated', `Schema "${notification.schemaName}" has been updated`);
        break;
      case 'schema_deleted':
        warning('Schema Deleted', `Schema "${notification.schemaName}" has been deleted`);
        break;
      case 'entity_created':
        success('Entity Created', `New ${notification.entityType} entity created`);
        break;
      case 'entity_updated':
        info('Entity Updated', `${notification.entityType} entity updated`);
        break;
      case 'entity_deleted':
        warning('Entity Deleted', `${notification.entityType} entity deleted`);
        break;
      default:
        console.warn('Unknown notification type:', notification.type);
    }
  }, [success, info, warning, notifyBell]);

  useEffect(() => {
    if (!isAuthenticated) return;

    notificationService.connect().catch(console.error);

    const unsubscribe = notificationService.subscribe(handleNotification);

    return () => {
      unsubscribe();
      notificationService.disconnect();
    };
  }, [isAuthenticated, handleNotification]);

  return null;
}

function AppRoutes() {
  const { isAuthenticated } = useAuth();

  return (
    <>
      <AuthHandler />
      <NotificationHandler />
      <Routes>
        <Route
          path="login"
          element={isAuthenticated ? <Navigate to="/" replace /> : <Login />}
        />
        
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <Layout />
            </ProtectedRoute>
          }
        >
          <Route index element={<Dashboard />} />
          
          <Route path="schemas" element={<Schemas />} />
          <Route path="schemas/new" element={<CreateSchema />} />
          <Route path="schemas/:name" element={<EditSchema />} />
          
          <Route path="environments" element={<Environments />} />
          
          <Route path="entities" element={<Entities />} />
          <Route path="entities/:entityType" element={<EntityList />} />
          <Route path="entities/:entityType/:id" element={<EntityList />} />
          <Route path="entities/:entityType/:id/edit" element={<EntityList />} />
          
          <Route
            path="users"
            element={
              <ProtectedRoute requiredPermission={Permissions.UsersRead}>
                <Users />
              </ProtectedRoute>
            }
          />
          <Route
            path="settings"
            element={
              <ProtectedRoute requiredPermission={Permissions.SettingsRead}>
                <Settings />
              </ProtectedRoute>
            }
          />
          <Route path="activity" element={<Activity />} />
          <Route
            path="mocks"
            element={
              <ProtectedRoute requiredPermission={Permissions.MocksRead}>
                <Mocks />
              </ProtectedRoute>
            }
          />
        </Route>
        
        <Route path="*" element={<Navigate to="/" replace={true} />} />
      </Routes>
    </>
  );
}

function App() {
  // Use Vite's BASE_URL which matches the configured base path
  // For GitHub Pages: '/test-service/'
  // For other deployments: '/testservice/ui/'
  const basename = import.meta.env.BASE_URL.replace(/\/$/, '');
  
  return (
    <BrowserRouter basename={basename}>
      <AuthProvider>
        <ToastProvider>
          <AppRoutes />
        </ToastProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
