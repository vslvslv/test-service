import React, { useState, useRef, useEffect, useLayoutEffect } from 'react';
import { Outlet, Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useToast } from '../contexts/ToastContext';
import NotificationBell, { NotificationBellRef } from './NotificationBell';
import {
  Home,
  Database,
  Layers,
  Server,
  Users,
  Settings,
  LogOut,
  Menu,
  X,
  Search,
  User as UserIcon
} from 'lucide-react';

const Layout: React.FC = () => {
  const [isSidebarOpen, setIsSidebarOpen] = useState(true);
  const { user, logout } = useAuth();
  const { setBellCallback } = useToast();
  const location = useLocation();
  const navigate = useNavigate();
  const bellRef = useRef<NotificationBellRef>(null);

  // Use useLayoutEffect to register callback synchronously after render
  useLayoutEffect(() => {
    // Use a small delay to ensure ref is populated
    const timer = setTimeout(() => {
      if (bellRef.current?.addNotification) {
        console.log('? Registering bell callback (useLayoutEffect)');
        setBellCallback(bellRef.current.addNotification);
      } else {
        console.log('? Bell ref not ready in useLayoutEffect, will retry...');
      }
    }, 50);

    return () => clearTimeout(timer);
  }, []); // Run once on mount

  // Also register on any changes to ensure it's set
  useEffect(() => {
    if (bellRef.current?.addNotification) {
      console.log('? Re-registering bell callback (useEffect)');
      setBellCallback(bellRef.current.addNotification);
    } else {
      console.log('? Bell ref still not ready in useEffect');
      // Keep trying
      const retryTimer = setInterval(() => {
        if (bellRef.current?.addNotification) {
          console.log('? Bell callback registered after retry');
          setBellCallback(bellRef.current.addNotification);
          clearInterval(retryTimer);
        }
      }, 100);

      // Stop trying after 5 seconds
      setTimeout(() => clearInterval(retryTimer), 5000);

      return () => clearInterval(retryTimer);
    }
  }, [setBellCallback]);

  const handleLogout = () => {
    logout();
    navigate('login');
  };

  const menuItems = [
    { icon: Home, label: 'Dashboard', path: '/' },
    { icon: Database, label: 'Entities', path: '/entities' },
    { icon: Layers, label: 'Schemas', path: '/schemas' },
    { icon: Server, label: 'Environments', path: '/environments' },
    { icon: Users, label: 'Users', path: '/users', adminOnly: true },
  ];

  const isActive = (path: string) => {
    return location.pathname === path;
  };

  return (
    <div className="min-h-screen bg-gray-900">
      {/* Top Navigation Bar */}
      <header className="bg-gray-800 border-b border-gray-700 fixed top-0 left-0 right-0 z-30 h-16">
        <div className="flex items-center justify-between h-full px-4">
          {/* Left side */}
          <div className="flex items-center gap-4">
            <button
              onClick={() => setIsSidebarOpen(!isSidebarOpen)}
              className="p-2 hover:bg-gray-700 rounded-lg transition-colors text-gray-400 hover:text-white"
            >
              {isSidebarOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
            </button>
            
            <div className="flex items-center gap-2">
              <Database className="w-6 h-6 text-blue-500" />
              <span className="text-white font-semibold text-lg">Test Service</span>
            </div>
          </div>

          {/* Search Bar */}
          <div className="hidden md:flex flex-1 max-w-2xl mx-8">
            <div className="relative w-full">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-500" />
              <input
                type="text"
                placeholder="Search schemas, entities, environments..."
                className="w-full pl-10 pr-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              />
            </div>
          </div>

          {/* Right side */}
          <div className="flex items-center gap-3">
            <NotificationBell ref={bellRef} />
            
            <div className="flex items-center gap-3 pl-3 border-l border-gray-700">
              <div className="flex items-center gap-2">
                <div className="w-8 h-8 bg-blue-600 rounded-full flex items-center justify-center">
                  <UserIcon className="w-4 h-4 text-white" />
                </div>
                <div className="hidden lg:block">
                  <p className="text-sm text-white font-medium">{user?.username}</p>
                  <p className="text-xs text-gray-400">{user?.role}</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </header>

      {/* Sidebar */}
      <aside
        className={`fixed left-0 top-16 bottom-0 bg-gray-800 border-r border-gray-700 transition-all duration-300 z-20 ${
          isSidebarOpen ? 'w-64' : 'w-0'
        } overflow-hidden`}
      >
        <nav className="p-4 space-y-1">
          {menuItems.map((item) => {
            if (item.adminOnly && user?.role !== 'Admin') return null;
            
            const Icon = item.icon;
            const active = isActive(item.path);

            return (
              <Link
                key={item.path}
                to={item.path}
                className={`flex items-center gap-3 px-4 py-3 rounded-lg transition-colors ${
                  active
                    ? 'bg-blue-600 text-white'
                    : 'text-gray-400 hover:bg-gray-700 hover:text-white'
                }`}
              >
                <Icon className="w-5 h-5 flex-shrink-0" />
                <span className="font-medium">{item.label}</span>
              </Link>
            );
          })}

          {/* Bottom Section */}
          <div className="absolute bottom-4 left-4 right-4 space-y-1 pt-4 border-t border-gray-700">
            <Link
              to="/settings"
              className={`flex items-center gap-3 px-4 py-3 rounded-lg transition-colors ${
                isActive('/settings')
                  ? 'bg-blue-600 text-white'
                  : 'text-gray-400 hover:bg-gray-700 hover:text-white'
              }`}
            >
              <Settings className="w-5 h-5 flex-shrink-0" />
              <span className="font-medium">Settings</span>
            </Link>

            <button
              onClick={handleLogout}
              className="w-full flex items-center gap-3 px-4 py-3 rounded-lg text-gray-400 hover:bg-red-600/10 hover:text-red-400 transition-colors"
            >
              <LogOut className="w-5 h-5 flex-shrink-0" />
              <span className="font-medium">Logout</span>
            </button>
          </div>
        </nav>
      </aside>

      {/* Main Content */}
      <main
        className={`transition-all duration-300 pt-16 ${
          isSidebarOpen ? 'ml-64' : 'ml-0'
        }`}
      >
        <div className="p-6">
          <Outlet />
        </div>
      </main>
    </div>
  );
};

export default Layout;
