import { BrowserRouter, Routes, Route, Navigate, useNavigate } from 'react-router-dom';
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
import './App.css';

function AuthHandler() {
  const navigate = useNavigate();

  useEffect(() => {
    const handleAuthError = () => {
      navigate('login', { replace: true });
    };

    window.addEventListener('auth-401', handleAuthError);
    return () => window.removeEventListener('auth-401', handleAuthError);
  }, [navigate]);

  return null;
}

function NotificationHandler() {
  const { success, info, warning, notifyBell } = useToast();
  const { isAuthenticated } = useAuth();

  const handleNotification = useCallback((notification: Notification) => {
    console.log('?? Notification received in handler:', notification);
    console.log('   Type:', notification.type);
    console.log('   Schema:', notification.schemaName);
    console.log('   Timestamp:', notification.timestamp);
    
    // Add to bell history
    notifyBell(notification);

    // Show toast notification
    switch (notification.type) {
      case 'schema_created':
        console.log('? Showing schema created toast');
        success('Schema Created', `Schema "${notification.schemaName}" has been created`);
        break;
      case 'schema_updated':
        console.log('? Showing schema updated toast');
        info('Schema Updated', `Schema "${notification.schemaName}" has been updated`);
        break;
      case 'schema_deleted':
        console.log('? Showing schema deleted toast');
        warning('Schema Deleted', `Schema "${notification.schemaName}" has been deleted`);
        break;
      case 'entity_created':
        console.log('? Showing entity created toast');
        success('Entity Created', `New ${notification.entityType} entity created`);
        break;
      case 'entity_updated':
        console.log('? Showing entity updated toast');
        info('Entity Updated', `${notification.entityType} entity updated`);
        break;
      case 'entity_deleted':
        console.log('? Showing entity deleted toast');
        warning('Entity Deleted', `${notification.entityType} entity deleted`);
        break;
      default:
        console.warn('?? Unknown notification type:', notification.type);
    }
  }, [success, info, warning, notifyBell]);

  useEffect(() => {
    if (!isAuthenticated) return;

    console.log('?? Connecting to SignalR notification hub...');

    // Connect to notification hub
    notificationService.connect().catch(console.error);

    // Subscribe to notifications
    const unsubscribe = notificationService.subscribe(handleNotification);

    return () => {
      console.log('?? Disconnecting from SignalR notification hub...');
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
          <Route path="entities/:entityType/new" element={<div className="text-white">Create Entity - Coming Soon</div>} />
          <Route path="entities/:entityType/:id" element={<EntityList />} />
          <Route path="entities/:entityType/:id/edit" element={<EntityList />} />
          
          <Route path="users" element={<div className="text-white">Users Page - Coming Soon</div>} />
          <Route path="settings" element={<Settings />} />
          <Route path="activity" element={<Activity />} />
        </Route>
        
        <Route path="*" element={<Navigate to="/" replace={true} />} />
      </Routes>
    </>
  );
}

function App() {
  // Use base path from import.meta to match vite config
  // On GitHub Pages: /test-service/
  // On other deployments: /testservice/ui/
  const basename = import.meta.env.BASE_URL.replace(/\/$/, ''); // Remove trailing slash
  
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
