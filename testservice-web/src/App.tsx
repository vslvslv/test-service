import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import Layout from './components/Layout';
import ProtectedRoute from './components/ProtectedRoute';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import './App.css';

function AppRoutes() {
  const { isAuthenticated } = useAuth();

  return (
    <Routes>
      <Route
        path="/login"
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
        
        {/* Placeholder routes for future pages */}
        <Route path="entities" element={<div className="text-white">Entities Page - Coming Soon</div>} />
        <Route path="schemas" element={<div className="text-white">Schemas Page - Coming Soon</div>} />
        <Route path="environments" element={<div className="text-white">Environments Page - Coming Soon</div>} />
        <Route path="users" element={<div className="text-white">Users Page - Coming Soon</div>} />
        <Route path="settings" element={<div className="text-white">Settings Page - Coming Soon</div>} />
      </Route>
      
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
