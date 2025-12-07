import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
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
        
        <Route path="schemas" element={<Schemas />} />
        <Route path="schemas/new" element={<CreateSchema />} />
        <Route path="schemas/:name" element={<EditSchema />} />
        
        <Route path="environments" element={<Environments />} />
        
        <Route path="entities" element={<Entities />} />
        <Route path="entities/:entityType" element={<EntityList />} />
        <Route path="entities/:entityType/new" element={<div className="text-white">Create Entity - Coming Soon</div>} />
        <Route path="entities/:entityType/:id" element={<div className="text-white">Entity Details - Coming Soon</div>} />
        
        <Route path="users" element={<div className="text-white">Users Page - Coming Soon</div>} />
        <Route path="settings" element={<div className="text-white">Settings Page - Coming Soon</div>} />
        <Route path="activity" element={<div className="text-white">Activity Page - Coming Soon</div>} />
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
